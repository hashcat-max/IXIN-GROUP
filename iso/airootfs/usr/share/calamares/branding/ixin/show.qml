import QtQuick 2.15
import calamares.slideshow 1.0

Presentation {
    id: presentation

    function nextSlide() { presentation.goToNextSlide(); }
    Timer { id: advanceTimer; interval: 7000; running: presentation.activatedInCalamares; repeat: true; onTriggered: nextSlide() }

    component IxinSlide: Slide {
        property string title
        property string body
        Rectangle { anchors.fill: parent; color: "#050505" }
        Image {
            id: logo
            source: "logo.png"; width: 96; height: 96; fillMode: Image.PreserveAspectFit
            anchors.horizontalCenter: parent.horizontalCenter; anchors.top: parent.top; anchors.topMargin: 40
        }
        Text {
            id: t; text: title; color: "#19f3a4"; font.pixelSize: 28; font.weight: Font.Light
            anchors.horizontalCenter: parent.horizontalCenter; anchors.top: logo.bottom; anchors.topMargin: 24
        }
        Text {
            text: body; color: "#e9ecef"; font.pixelSize: 16; wrapMode: Text.WordWrap
            horizontalAlignment: Text.AlignHCenter; width: parent.width * 0.75
            anchors.horizontalCenter: parent.horizontalCenter; anchors.top: t.bottom; anchors.topMargin: 18
        }
    }

    IxinSlide { title: "Bienvenue dans IXIN OS"; body: "Un système basé sur Arch Linux, pensé pour la sécurité, la vie privée et la performance — dans l'esprit d'IXIN." }
    IxinSlide { title: "Sécurité par défaut"; body: "Pare-feu actif, AppArmor, DNS chiffré, adresse MAC Wi-Fi aléatoire, chiffrement du disque LUKS. Tapez « ixin-secure » pour voir l'état." }
    IxinSlide { title: "Jeux"; body: "Steam + Proton, Lutris, Heroic, Bottles, Wine, GameMode, MangoHud et Gamescope sont préinstallés. Noyau Zen optimisé pour la réactivité." }
    IxinSlide { title: "Vie privée"; body: "Navigateur Tor, Signal, KeePassXC, VeraCrypt, OpenSnitch, nettoyage des métadonnées (mat2) et BleachBit." }
    IxinSlide { title: "Restez à jour"; body: "« ixin-update » met à jour le système, les Flatpak, les firmwares et signale les paquets vulnérables. Les instantanés Btrfs (Timeshift) permettent de revenir en arrière." }

    function onActivate() { presentation.currentSlide = 0; }
    function onLeave() { }
}
