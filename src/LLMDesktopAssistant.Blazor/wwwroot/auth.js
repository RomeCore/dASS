// Auth helper functions for WebUI
// Handles authentication via cookie-based API

window.checkAuth = async function () {
	try {
		const response = await fetch('/api/auth/me');
		return response.ok;
	} catch {
		return false;
	}
};

window.loginUser = async function (login, password, masterPassword) {
	try {
		const response = await fetch('/api/auth/login', {
			method: 'POST',
			headers: { 'Content-Type': 'application/json' },
			body: JSON.stringify({ login, password, masterPassword })
		});
		return response.ok;
	} catch {
		return false;
	}
};

window.logoutUser = async function () {
	try {
		await fetch('/api/auth/logout', {
			method: 'POST',
			headers: { 'Content-Type': 'application/json' }
		});
	} catch {
		// ignore
	}
	window.location.replace('/login');
};

window.checkMasterAuth = async function () {
	try {
		const response = await fetch('/api/auth/master/status');
		return response.ok;
	} catch {
		return false;
	}
};

window.enterMasterPassword = async function (password) {
	try {
		const response = await fetch('/api/auth/master/enter', {
			method: 'POST',
			headers: { 'Content-Type': 'application/json' },
			body: JSON.stringify({ password })
		});
		return response.ok;
	} catch {
		return false;
	}
};

window.locationReplace = function (url) {
	window.location.replace(url);
};

window.selectProfileImage = function () {
	return new Promise(function (resolve) {
		var input = document.createElement('input');
		input.type = 'file';
		input.accept = 'image/*';

		// Safety timeout - if the user never picks a file, resolve with null
		var safetyTimer = setTimeout(function () {
			cleanup();
			resolve(null);
		}, 60000);

		function cleanup() {
			clearTimeout(safetyTimer);
			window.removeEventListener('focus', onFocusCapture);
		}

		// Detect cancellation: after input.click() the browser loses focus to the dialog,
		// when the dialog closes (Cancel or ESC), focus returns to the window.
		function onFocusCapture() {
			setTimeout(function () {
				cleanup();
				if (!input.files || input.files.length === 0) {
					resolve(null);
				}
			}, 300);
		}
		window.addEventListener('focus', onFocusCapture, { once: true });

		input.onchange = async function (e) {
			cleanup();

			var file = e.target.files?.[0];
			if (!file) {
				resolve(null);
				return;
			}

			try {
				// Decode the image at full resolution
				var img = await createImageBitmap(file);

				// Resize to 128x128 with center crop
				var canvas = document.createElement('canvas');
				canvas.width = 128;
				canvas.height = 128;
				var ctx = canvas.getContext('2d');

				var size = Math.min(img.width, img.height);
				var sx = (img.width - size) / 2;
				var sy = (img.height - size) / 2;
				ctx.drawImage(img, sx, sy, size, size, 0, 0, 128, 128);

				// Free the decoded bitmap as early as possible
				img.close();

				// Encode as JPEG (faster and smaller than PNG for photos)
				var base64 = canvas.toDataURL('image/jpeg', 0.85).split(',')[1];
				resolve(base64);
			} catch (err) {
				// If anything fails (bad image, memory, etc.), silently bail out
				resolve(null);
			}
		};

		input.click();
	});
};
