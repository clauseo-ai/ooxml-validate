#!/bin/sh
# Fills the placeholders of packaging/homebrew/ooxml-validate.rb from the release's checksum files and writes the result.
# Usage: sh packaging/homebrew/fill-formula.sh <assets dir> <version> <out file>
# It drops the template-only "#@" comment lines. It reads ooxml-validate-<rid>.tar.gz.sha256 for the four macOS and Linux targets. It fails if a checksum file is
# missing or malformed, or if a placeholder is left. It writes the file only; nothing is pushed to a tap.
set -eu

if [ "$#" -ne 3 ]; then
  echo "usage: sh packaging/homebrew/fill-formula.sh <assets dir> <version> <out file>" >&2
  exit 2
fi
assets=$1
version=$2
out=$3

# The version without the tag's "v": 0.1.0, not v0.1.0, since the url template adds the "v" itself.
case "$version" in
  ''|v*|*[!0-9A-Za-z.+-]*) echo "fill-formula: bad version: $version (give 0.1.0, not v0.1.0)" >&2; exit 2 ;;
esac

template="$(cd "$(dirname "$0")" && pwd)/ooxml-validate.rb"
tmp="$out.tmp"
# The "#@" comment lines describe the template only; the filled formula leaves them out.
sed '/^#@/d' "$template" > "$tmp"
sed "s/@VERSION@/$version/g" "$tmp" > "$tmp.2" && mv "$tmp.2" "$tmp"

for rid in osx-arm64 osx-x64 linux-arm64 linux-x64; do
  file="$assets/ooxml-validate-$rid.tar.gz.sha256"
  if [ ! -f "$file" ]; then
    echo "fill-formula: missing $file" >&2
    rm -f "$tmp"
    exit 1
  fi
  hash=$(awk '{print $1}' "$file")
  name=$(awk '{print $2}' "$file")
  if ! printf '%s\n' "$hash" | grep -Eq '^[0-9a-f]{64}$'; then
    echo "fill-formula: $file does not hold a SHA-256 hash" >&2
    rm -f "$tmp"
    exit 1
  fi
  if [ "$name" != "ooxml-validate-$rid.tar.gz" ]; then
    echo "fill-formula: $file names $name, not ooxml-validate-$rid.tar.gz" >&2
    rm -f "$tmp"
    exit 1
  fi
  key=$(printf '%s' "$rid" | tr 'a-z-' 'A-Z_')
  sed "s/@SHA256_$key@/$hash/g" "$tmp" > "$tmp.2" && mv "$tmp.2" "$tmp"
done

if grep -Eq '@[A-Z0-9_]+@' "$tmp"; then
  echo "fill-formula: the formula still has placeholders:" >&2
  grep -Eo '@[A-Z0-9_]+@' "$tmp" | sort -u >&2
  rm -f "$tmp"
  exit 1
fi

mv "$tmp" "$out"
echo "fill-formula: wrote $out for version $version"
