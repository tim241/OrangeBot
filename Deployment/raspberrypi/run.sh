#!/usr/bin/env bash
set -e

config=$1
current_dir="$(realpath "$(dirname "$0")")"

export ORANGEBOT_CONFIG=$(cat "$config")

"$current_dir"/OrangeBot
