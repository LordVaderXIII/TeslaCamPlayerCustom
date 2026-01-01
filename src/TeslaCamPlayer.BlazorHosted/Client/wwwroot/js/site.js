function getProperty(object, property) {
	return object[property];
}

function setProperty(object, property, value) {
	object[property] = value;
}

function getSliderPercentage(clientX, element) {
    var rect = element.getBoundingClientRect();
    var x = clientX - rect.left;
    var percentage = x / rect.width;
    return Math.max(0, Math.min(1, percentage));
}

function syncVideos(mainVideo, otherVideos, threshold) {
    if (!mainVideo) return;

    var mainTime = mainVideo.currentTime;
    if (isNaN(mainTime)) return;

    for (var i = 0; i < otherVideos.length; i++) {
        var video = otherVideos[i];
        if (!video) continue;

        var diff = Math.abs(video.currentTime - mainTime);
        if (diff > threshold) {
            video.currentTime = mainTime;
        }
    }
}
