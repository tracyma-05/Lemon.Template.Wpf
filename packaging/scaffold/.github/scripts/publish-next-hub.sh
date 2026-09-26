#!/usr/bin/env bash
# Publishes release packages to Next.Hub as one version with a package per platform.
#
#   publish-next-hub.sh <version> <release-notes-file> <package>...
#
# Each package's platform is read from its file name (MyApp-1.2.0-osx-arm64.zip → osx-arm64); a
# name without one is published as "any". Uploads go in chunks, so packages larger than a proxy's request
# limit (Cloudflare: 100 MB) still get through, and a chunk that fails is resent from the same offset.
#
# Needs curl, jq and sha256sum (all on GitHub's Ubuntu runners) and these environment variables:
#   NEXT_HUB_URL    API base, e.g. https://example.com/hub-api/
#   NEXT_HUB_APP    the app's key in Next.Hub
#   NEXT_HUB_TOKEN  a publish token for that app (created on the app's page; never printed)
set -euo pipefail

if [ "$#" -lt 3 ]; then
    echo "usage: $0 <version> <release-notes-file> <package>..." >&2
    exit 2
fi

version="$1"
notes_file="$2"
shift 2

: "${NEXT_HUB_URL:?NEXT_HUB_URL is not set}"
: "${NEXT_HUB_APP:?NEXT_HUB_APP is not set}"
: "${NEXT_HUB_TOKEN:?NEXT_HUB_TOKEN is not set}"

api="${NEXT_HUB_URL%/}/api/app/desktop-apps/${NEXT_HUB_APP}"
chunk_limit=$((16 * 1024 * 1024))

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

# The token goes in a header file rather than on the command line, so it never shows up in a process list
# or in a trace of this script.
(umask 077 && printf 'X-Publish-Token: %s\n' "$NEXT_HUB_TOKEN" > "$work/auth")

# Chunk uploads and starting an upload are safe to repeat, so they are retried; publishing is not.
request() {
    local method="$1" url="$2"
    shift 2
    curl --fail-with-body --silent --show-error --retry 4 --retry-all-errors --retry-delay 5 \
        -X "$method" -H @"$work/auth" "$@" "$url"
}

packages='[]'
for file in "$@"; do
    name="$(basename "$file")"
    platform="$(sed -nE 's/.*[-_.]((win|osx|linux)-(x64|x86|arm64))\.[A-Za-z0-9.]+$/\1/p' <<<"$name")"
    platform="${platform:-any}"
    size="$(stat -c %s "$file")"
    sha256="$(sha256sum "$file" | cut -d ' ' -f 1)"

    started="$(request POST "$api/uploads")"
    upload_id="$(jq -er .uploadId <<<"$started")"
    max_chunk="$(jq -er .maxChunkSize <<<"$started")"
    chunk=$((max_chunk < chunk_limit ? max_chunk : chunk_limit))

    echo "Uploading $name ($platform, $size bytes)"
    offset=0
    while [ "$offset" -lt "$size" ]; do
        tail -c +"$((offset + 1))" "$file" | head -c "$chunk" > "$work/chunk"
        received="$(request PUT "$api/uploads/$upload_id?offset=$offset" \
            -H "Content-Type: application/octet-stream" --data-binary @"$work/chunk" | jq -er .received)"
        if [ "$received" -le "$offset" ]; then
            echo "Next.Hub stopped accepting $name at byte $offset." >&2
            exit 1
        fi
        offset="$received"
        echo "  $offset / $size"
    done

    packages="$(jq -c --arg id "$upload_id" --arg platform "$platform" --arg name "$name" --arg sha "$sha256" \
        '. + [{uploadId: $id, platform: $platform, fileName: $name, sha256: $sha}]' <<<"$packages")"
done

# Next.Hub keeps up to 4000 characters of notes.
jq -n --arg version "$version" --rawfile notes "$notes_file" --argjson packages "$packages" \
    '{version: $version, releaseNotes: ($notes | .[0:3900] | if test("^\\s*$") then null else . end), packages: $packages}' \
    > "$work/release.json"

echo "Publishing $NEXT_HUB_APP $version"
curl --fail-with-body --silent --show-error -X POST -H @"$work/auth" \
    -H "Content-Type: application/json" --data-binary @"$work/release.json" "$api/releases" | jq .
