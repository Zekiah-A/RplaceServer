#!/bin/bash
# This script will alow the rplace server to always run as a background on your device and auto start on reboot using a SystemD unit service (linux only)

if [ -z "$1" ]
then
    echo -e "\x1b[31mPlease input the path to the rplace server directory as an arguument e.g '/home/pi/RplaceServer/TKOfficial'"
    exit 0
fi

dotnet_dir=$(which dotnet)

echo -e "
[Unit]
Description=Main place Websocket and HTTP server daemon.
After=network.target
[Service]
Type=simple
StandardInput=tty-force
TTYVHangup=yes
TTYPath=/dev/tty20
TTYReset=yes
Environment=DOTNET_CLI_HOME=/tmp
WorkingDirectory=$1
ExecStart=
ExecStart=$dotnet_dir run
Restart=always
RestartSec=2
[Install]
WantedBy=multi-user.target
" | sudo tee -a /etc/systemd/system/place.service
sudo systemctl daemon-reload

sudo systemctl enable place.service
sudo systemctl start place.service

echo "Task completed. Use 'systemctl status place.service' to monitor the status of the server process."
echo "Reccomended: With conspy, you can use 'sudo conspy 20' to view the virtual console of the server process, more info can be found at https://conspy.sourceforge.net/"
