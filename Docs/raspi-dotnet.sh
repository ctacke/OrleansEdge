#!/bin/bash

add_to_bashrc() {
    local string="$1"
    if ! grep -q "$string" "$HOME/.bashrc"; then
        echo "$string" >> $HOME/.bashrc
    fi
}

wget --inet4-only https://dot.net/v1/dotnet-install.sh -O - | bash /dev/stdin --version 8.0.416
add_to_bashrc "export DOTNET_ROOT=\$HOME/.dotnet"
add_to_bashrc "export PATH=\$PATH:\$HOME/.dotnet"
source ~/.bashrc
