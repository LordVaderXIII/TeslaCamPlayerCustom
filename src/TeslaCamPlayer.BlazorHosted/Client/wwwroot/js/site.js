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