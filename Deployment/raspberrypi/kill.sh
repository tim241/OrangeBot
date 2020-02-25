#!/usr/bin/env bash

set -e

current_dir=$(realpath "$(dirname "$0")")

for config in "$current_dir/configs"/*
do
    name=$(basename "$config")

    if [ "$(screen -ls | grep "$name")" ]
    then
        echo "-> killing '$name'"
        screen -S "$name" -X quit
    fi
done
