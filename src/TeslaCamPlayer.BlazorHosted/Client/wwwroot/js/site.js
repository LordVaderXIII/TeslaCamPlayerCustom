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

function registerTimeUpdateObserver(element, dotNetRef, interval) {
    if (!element || !dotNetRef) return;

    // Cleanup existing if any
    unregisterTimeUpdateObserver(element);

    var lastTime = 0;
    var handler = function() {
        var now = Date.now();
        if (now - lastTime < interval) return;
        lastTime = now;
        dotNetRef.invokeMethodAsync('HandleTimeUpdate', element.currentTime);
    };

    element.addEventListener('timeupdate', handler);
    element._blazorTimeUpdateHandler = handler;
}

function unregisterTimeUpdateObserver(element) {
    if (!element || !element._blazorTimeUpdateHandler) return;
    element.removeEventListener('timeupdate', element._blazorTimeUpdateHandler);
    delete element._blazorTimeUpdateHandler;
}
