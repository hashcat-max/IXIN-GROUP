#!/usr/bin/env bash
# shellcheck disable=SC2034
# IXIN OS — profil archiso

iso_name="ixin-os"
iso_label="IXIN_$(date --date="@${SOURCE_DATE_EPOCH:-$(date +%s)}" +%Y%m)"
iso_publisher="IXIN Group <https://getixin.com>"
iso_application="IXIN OS Live / Installer"
iso_version="$(date --date="@${SOURCE_DATE_EPOCH:-$(date +%s)}" +%Y.%m.%d)"
install_dir="ixin"
buildmodes=('iso')
bootmodes=('bios.syslinux'
           'uefi.systemd-boot')
arch="x86_64"
pacman_conf="pacman.conf"
airootfs_image_type="squashfs"
airootfs_image_tool_options=('-comp' 'zstd' '-Xcompression-level' '19' '-b' '1M')
bootstrap_tarball_compression=('zstd' '-c' '-T0' '--long' '-19')
file_permissions=(
  ["/etc/shadow"]="0:0:400"
  ["/etc/sudoers.d/10-ixin-live"]="0:0:440"
  ["/root"]="0:0:750"
  ["/root/.gnupg"]="0:0:700"
  ["/usr/local/bin/livecd-sound"]="0:0:755"
  ["/root/customize_airootfs.sh"]="0:0:755"
  ["/usr/local/bin/ixin-postinstall"]="0:0:755"
  ["/usr/local/bin/ixin-install"]="0:0:755"
  ["/usr/local/bin/ixin-setup-windows"]="0:0:755"
)
