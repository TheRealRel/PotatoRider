Riding fixes and key steering for Valheim mounts. It runs only in your own game: the server doesn't need it, and anyone without it rides the usual way, on the same server. It's built for the Odin Horse first, and more mounts will follow.

![](https://raw.githubusercontent.com/TheRealRel/PotatoRider/main/docs/images/icon-horseshoe.png)

**Steer with the keys, not the mouse.** In the base game a mount goes wherever the camera looks, and A and D do nothing. With PotatoRider the keys steer the Odin Horse and the mouse only moves the camera, so you can look around, or aim, without turning the horse.

- W walks, building up gently to a trot. Shift gallops.
- Let go and the horse stops.
- A and D turn it at its own turn speed, with its turning legs, and standing still it turns in place.

The camera turns with the horse, so a turn doesn't swing your view away.

<!-- screenshot: riding while turning -->

![](https://raw.githubusercontent.com/TheRealRel/PotatoRider/main/docs/images/icon-bow.png)

**Shoot from the saddle.** The Odin Horse lets you draw a bow while riding, within an arc either side of the horse. Before, the camera could turn past that arc while the arrow couldn't, so the crosshair pointed where no arrow could go. Now, while the bow is drawn, the camera stops at the edge of the arc, and an arrow loosed there flies along the crosshair. With Better Archery installed, the rider no longer flickers while drawing.

<!-- screenshot: the bow drawn at the edge of the arc -->

![](https://raw.githubusercontent.com/TheRealRel/PotatoRider/main/docs/images/icon-saddle.png)

**A level camera and a calm horse.** Attacking from the saddle no longer rolls the camera onto its side. And a ridden Odin Horse no longer rears up to attack on its own, throwing you around mid-fight. Unridden, it fights as before.

![](https://raw.githubusercontent.com/TheRealRel/PotatoRider/main/docs/images/icon-horse.png)

**The right picture on the mount panel.** The panel by your health and stamina bars showed a Lox for every mount. Now it shows the animal you're riding.

<!-- screenshot: the mount panel showing the horse -->

## Settings

Every part has its own setting, under PotatoRider in Configuration Manager, and changes take effect while you ride. They're your own, not the server's.

| Setting | Default | What it does |
|---|---|---|
| Steer with the keys | On | A and D turn the horse, W walks, Shift gallops, let go to stop. Off: the base game's way, where the horse goes where the camera looks. |
| Turn speed percent | 100 | How fast A and D turn the horse, as a percent of its own turn speed (25-300). |
| Seconds to full trot | 0.75 | How long W takes to build up to a trot, and to slow back down. 0 keeps the horse's own quicker build-up. |
| Turns with the horse | On | The camera turns when the horse turns. Off: it holds its direction while the horse turns under you. |
| Camera stops at the bow arc | On | While a bow is drawn, the camera stops at the edge of the Odin Horse's bow arc. |
| Margin inside the arc | 0 | Degrees the camera stops short of the arc's edge (0-20). |

**Tip for servers with the Odin Horse.** Its "Speed Modifier" setting is really the horse's walking speed, in metres a second. At the default of 1, W barely moves it. Around 3 makes W a trot, which fits the steering here. Its bow arc settings ("Bow Turn Left", "Bow Turn Right", "Bow Aim Up Down") decide where the camera stops: we use 110, 110 and 55.

Bug reports go to [GitHub Issues](https://github.com/TheRealRel/PotatoRider/issues), where the source lives too, along with a write-up of how riding works in Valheim ([docs/riding.md](https://github.com/TheRealRel/PotatoRider/blob/main/docs/riding.md)). Questions are welcome on Discord: [relanebeis](https://discord.com/users/373574370267889674).

Needs [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/) and [Jötunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/). The steering and the bow are for the [Odin Horse](https://thunderstore.io/c/valheim/p/OdinPlus/OdinHorse/). The level camera and the mount picture work on any mount.

The code is under the MIT License. The icon, page icons and screenshots are © TheRealRel under [CC BY-NC 4.0](https://creativecommons.org/licenses/by-nc/4.0/): free to share and adapt with credit, but not for commercial use. See LICENSE for the details.
