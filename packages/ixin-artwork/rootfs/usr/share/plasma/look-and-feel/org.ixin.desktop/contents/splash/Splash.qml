import QtQuick 2.15

Rectangle {
    id: root
    color: "#050505"
    property int stage

    onStageChanged: if (stage == 2) introAnimation.running = true

    Image {
        id: logo
        anchors.centerIn: parent
        source: "/usr/share/ixin/branding/ixin-logo-256.png"
        sourceSize.width: 160; sourceSize.height: 160
        opacity: 0
        OpacityAnimator on opacity { id: introAnimation; running: false; from: 0; to: 1; duration: 900; easing.type: Easing.OutCubic }
    }
    Text {
        anchors.top: logo.bottom; anchors.topMargin: 28; anchors.horizontalCenter: parent.horizontalCenter
        text: "IXIN OS"; color: "#e9ecef"; font.pixelSize: 26; font.weight: Font.Light; font.letterSpacing: 10
    }
    Rectangle {
        anchors.bottom: parent.bottom; anchors.bottomMargin: parent.height / 6
        anchors.horizontalCenter: parent.horizontalCenter
        width: 220; height: 2; radius: 1; color: "#1a1d22"
        Rectangle {
            height: parent.height; radius: 1
            width: parent.width * Math.min(1, (root.stage + 1) / 6)
            gradient: Gradient { orientation: Gradient.Horizontal
                GradientStop { position: 0; color: "#19f3a4" } GradientStop { position: 1; color: "#1c6bff" } }
            Behavior on width { NumberAnimation { duration: 400; easing.type: Easing.OutQuad } }
        }
    }
}
