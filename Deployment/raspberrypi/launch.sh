#!/usr/bin/env bash

set -e

current_dir=$(realpath "$(dirname "$0")")

for config in "$current_dir/configs"/*
do
    name=$(basename "$config")

    echo "-> launching '$name'"

    screen -dmS "$name" bash -c "$current_dir/run.sh $config; exec bash"

done
