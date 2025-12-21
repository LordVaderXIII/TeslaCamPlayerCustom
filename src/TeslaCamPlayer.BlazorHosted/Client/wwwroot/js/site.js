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
function getVideoTimes(elements) {
	if (!Array.isArray(elements)) return [];
	return elements.map(e => (e && typeof e.currentTime === 'number') ? e.currentTime : -1);
}
