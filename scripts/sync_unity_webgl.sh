#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
build_dir="${repo_root}/unity/HeavySuvPrototype/Builds/WebGL"
deploy_dir="${repo_root}/deploy/webgl"

if [[ ! -f "${build_dir}/index.html" || ! -d "${build_dir}/Build" ]]; then
  echo "Unity WebGL build not found at ${build_dir}" >&2
  echo "Build it using the command documented in unity/HeavySuvPrototype/README.md." >&2
  exit 1
fi

rm -rf "${deploy_dir}"
mkdir -p "${deploy_dir}"
rsync -a \
  --exclude='*_BurstDebugInformation_DoNotShip/' \
  "${build_dir}/" "${deploy_dir}/"

build_revision="$({
  cksum "${deploy_dir}"/Build/*
} | cksum | awk '{print $1}')"

sed \
  -e "s#/WebGL.loader.js#/WebGL.loader.js?v=${build_revision}#g" \
  -e "s#/WebGL.data.gz#/WebGL.data.gz?v=${build_revision}#g" \
  -e "s#/WebGL.framework.js.gz#/WebGL.framework.js.gz?v=${build_revision}#g" \
  -e "s#/WebGL.wasm.gz#/WebGL.wasm.gz?v=${build_revision}#g" \
  "${deploy_dir}/index.html" > "${deploy_dir}/index.html.tmp"
mv "${deploy_dir}/index.html.tmp" "${deploy_dir}/index.html"

echo "Synced Unity WebGL build ${build_revision} to ${deploy_dir}"
