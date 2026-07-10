# Convoy Rally

This repository contains the browser prototype and the Unity WebGL version of
Convoy Rally. The deployable game image serves a checked-in WebGL snapshot from
`deploy/webgl` with Nginx.

## Multiplayer invites

Opening the game without a `join` query creates a fresh Unity Relay allocation.
The host copies the invite link shown in the multiplayer HUD and sends it to the
other drivers. Invite links use the Relay code as `?join=<relay-code>`; an
expired invite stops with an option to create a new room instead of reconnecting
forever.

Phone browsers use a landscape-only touch layout: steering on the left,
gas/brake/boost on the right, and a collapsible `CAR` panel for selector,
AWD/RWD, sound, and respawn controls. The top-right toolbar can independently
show or hide driving controls and the invite link. Touch `BOOST` also applies
gas, while keyboard Shift remains boost-only. Touch-capable desktop browsers
keep the toolbar but start with the driving controls hidden.

## Update the WebGL deployment snapshot

Build the Unity project as documented in
`unity/HeavySuvPrototype/README.md`, then synchronize the generated files:

```sh
./scripts/sync_unity_webgl.sh
```

Commit the resulting `deploy/webgl` changes together with the game source. This
keeps container builds reproducible without requiring a Unity license in GitHub
Actions. The sync script adds a content revision to Unity's fixed build URLs,
and Nginx requires revalidation so browsers do not mix incompatible multiplayer
builds.

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
