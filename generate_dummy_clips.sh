#!/bin/bash

# Directory structure
BASE_DIR="dummy_clips/TeslaCam"
SENTRY_DIR="$BASE_DIR/SentryClips/2024-05-20_12-00-00"

mkdir -p "$SENTRY_DIR"

# Function to create dummy mp4
create_clip() {
    filename=$1
    if [ ! -f "$SENTRY_DIR/$filename" ]; then
        # Create a 1-second black video using ffmpeg
        # Requires ffmpeg installed. If not, we can just touch a file,
        # but the app checks duration with ffprobe, so valid mp4 is better.

        # If ffmpeg is available
        if command -v ffmpeg &> /dev/null; then
            ffmpeg -f lavfi -i color=c=black:s=640x480:d=1 -c:v libx264 -tune stillimage -pix_fmt yuv420p "$SENTRY_DIR/$filename" > /dev/null 2>&1
            echo "Created $filename"
        else
            echo "ffmpeg not found, creating empty file $filename (app might fail to parse duration)"
            touch "$SENTRY_DIR/$filename"
        fi
    fi
}

# 2024-05-20_12-00-00
DATE_PREFIX="2024-05-20_12-00-00"

# Standard cameras
create_clip "${DATE_PREFIX}-front.mp4"
create_clip "${DATE_PREFIX}-back.mp4"
create_clip "${DATE_PREFIX}-left_repeater.mp4"
create_clip "${DATE_PREFIX}-right_repeater.mp4"

# New cameras
create_clip "${DATE_PREFIX}-left_pillar.mp4"
create_clip "${DATE_PREFIX}-right_pillar.mp4"
create_clip "${DATE_PREFIX}-fisheye.mp4"
create_clip "${DATE_PREFIX}-narrow.mp4"

# Create event.json
echo '{"timestamp":"2024-05-20T12:00:00","city":"Test City","est_lat":0,"est_lon":0,"reason":"sentry_aware_object_detection"}' > "$SENTRY_DIR/event.json"

echo "Dummy clips generated in $SENTRY_DIR"
