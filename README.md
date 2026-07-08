# Convoy Rally

This repository contains the browser prototype and the Unity WebGL version of
Convoy Rally. The deployable game image serves a checked-in WebGL snapshot from
`deploy/webgl` with Nginx.

## Update the WebGL deployment snapshot

Build the Unity project as documented in
`unity/HeavySuvPrototype/README.md`, then synchronize the generated files:

```sh
./scripts/sync_unity_webgl.sh
```

Commit the resulting `deploy/webgl` changes together with the game source. This
keeps container builds reproducible without requiring a Unity license in GitHub
Actions.

## Build the container locally

```sh
docker build -t game-drifting:local .
docker run --rm -p 8080:80 game-drifting:local
```

Open `http://localhost:8080` and verify the health endpoint with:

```sh
curl http://localhost:8080/healthz
```

## GitHub Container Registry

The `Publish container image` GitHub Actions workflow builds and pushes the
container automatically whenever `main` is updated. It uses GitHub's generated
`GITHUB_TOKEN`, so no registry password or personal access token is required.
The final image contains Nginx and is built explicitly for `linux/amd64`, which
matches the x86-64 Chronos k3s nodes.

Images are published as:

```text
ghcr.io/kibk/game-drifting:main
ghcr.io/kibk/game-drifting:sha-<commit>
ghcr.io/kibk/game-drifting:latest
```

You can also start the workflow manually from the repository's **Actions** tab.
After its first successful run, open the package settings on GitHub and choose
whether the package should remain private or become public. A private package
requires an image pull secret in Kubernetes; a public package can be pulled by
Chronos without registry credentials.
