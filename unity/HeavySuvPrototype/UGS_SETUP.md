# Unity Gaming Services Setup

The multiplayer code is complete, but this repository intentionally does not contain a Unity Cloud project ID or account credentials.

1. Open `unity/HeavySuvPrototype` in Unity Hub with Unity 6.5.
2. Sign in to Unity Hub.
3. Open **Edit > Project Settings > Services** and link the project to a Unity Cloud project.
4. In the Unity Dashboard, enable **Authentication**, **Multiplayer Sessions**, and **Relay** for the linked environment.
5. Keep anonymous authentication enabled. The prototype creates a temporary profile for each browser load.
6. Regenerate the scene and build WebGL using the commands in `README.md`.

When no cloud project is linked, the game deliberately starts a one-player local host and displays a setup message instead of becoming unplayable.

Each browser opened without a `join` query creates a fresh private session supporting eight active drivers. The host shares the displayed `?join=<session-code>` link, and invited browsers join by that session code. Relay client-host networking uses secure WebSockets in `europe-north1` and preserves that region across host migration. Bump the Netcode protocol version whenever a release makes network-incompatible prefab or behavior changes. Host migration uses a one-byte reset marker because this prototype intentionally resets cars after host loss.
