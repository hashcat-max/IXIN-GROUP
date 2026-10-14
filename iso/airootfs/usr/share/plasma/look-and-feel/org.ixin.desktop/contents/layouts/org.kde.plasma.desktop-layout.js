// IXIN OS — disposition du bureau : barre des tâches en bas, style Windows 11
// [ espace | Démarrer IXIN | applis épinglées + fenêtres | espace | zone de notification | horloge | bureau ]

var panel = new Panel
panel.location = "bottom"
panel.height = 2 * Math.floor(gridUnit * 2.6 / 2)
panel.alignment = "center"
panel.lengthMode = "fill"
panel.hiding = "none"
panel.floating = false

// Espace flexible à gauche (centre le menu + les tâches)
panel.addWidget("org.kde.plasma.panelspacer")

// Menu Démarrer avec le logo IXIN
var kickoff = panel.addWidget("org.kde.plasma.kickoff")
kickoff.currentConfigGroup = ["General"]
kickoff.writeConfig("icon", "ixin")
kickoff.writeConfig("favoritesDisplay", "0")
kickoff.writeConfig("showActionButtonCaptions", "true")
kickoff.currentConfigGroup = ["Shortcuts"]
kickoff.writeConfig("global", "Alt+F1")

// Recherche (comme la loupe de Windows)
panel.addWidget("org.kde.milou")

// Barre des tâches (icônes seules, applications épinglées)
var tasks = panel.addWidget("org.kde.plasma.icontasks")
tasks.currentConfigGroup = ["General"]
tasks.writeConfig("fill", "false")
tasks.writeConfig("iconSpacing", "2")
tasks.writeConfig("launchers", [
    "applications:org.kde.dolphin.desktop",
    "applications:firefox.desktop",
    "applications:steam.desktop",
    "applications:org.kde.konsole.desktop",
    "applications:org.keepassxc.KeePassXC.desktop",
    "applications:ixin-center.desktop"
].join(","))

// Espace flexible à droite
panel.addWidget("org.kde.plasma.panelspacer")

panel.addWidget("org.kde.plasma.systemtray")
var clock = panel.addWidget("org.kde.plasma.digitalclock")
clock.currentConfigGroup = ["Appearance"]
clock.writeConfig("showDate", "true")
clock.writeConfig("dateFormat", "shortDate")
panel.addWidget("org.kde.plasma.showdesktop")

// Fond d'écran IXIN sur tous les bureaux
var allDesktops = desktops()
for (var i = 0; i < allDesktops.length; i++) {
    var d = allDesktops[i]
    d.wallpaperPlugin = "org.kde.image"
    d.currentConfigGroup = ["Wallpaper", "org.kde.image", "General"]
    d.writeConfig("Image", "file:///usr/share/ixin/wallpapers/ixin-void.png")
    d.writeConfig("FillMode", "2")
}
