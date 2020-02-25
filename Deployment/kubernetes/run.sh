#!/usr/bin/env bash
set -e

# get pod id
export POD_ID=${HOSTNAME##*-}

# set config
case "$POD_ID" in
    "0")
        export ORANGEBOT_CONFIG='@@POD0_CONFIG@@';;
    "1")
        export ORANGEBOT_CONFIG='@@POD1_CONFIG@@';;
esac

# start the bot
./OrangeBot