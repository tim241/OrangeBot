#!/usr/bin/env bash
#
# This script deploys OrangeBot to my raspberry pi 3b+
#
set -e

TARGET_USER=tim
TARGET_ADDR=192.168.2.199

OUT_DIR="$(pwd)/out"

rm -rf "$OUT_DIR"

dotnet publish OrangeBot/OrangeBot.csproj \
    -c Release \
    -r linux-arm \
    /p:PublishSingleFile=true \
    /p:PublishTrimmed=true \
    -o "$OUT_DIR"

# Kill running bot(s)
ssh "$TARGET_USER@$TARGET_ADDR" "./Deployment/kill.sh"

# Copy build result to target
scp out/OrangeBot "$TARGET_USER@$TARGET_ADDR:./Deployment/OrangeBot"

# Launch build result on target
ssh "$TARGET_USER@$TARGET_ADDR" "./Deployment/launch.sh"