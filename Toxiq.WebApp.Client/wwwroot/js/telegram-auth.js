window.telegramAuthUtils = {

    // Check if we have valid Telegram WebApp init data
    hasValidInitData: function () {
        try {
            if (!window.Telegram || !window.Telegram.WebApp) {
                console.log('Telegram WebApp not available');
                return false;
            }

            const initData = window.Telegram.WebApp.initData;
            console.log('Telegram init data available:', !!initData);

            return !!initData && initData.length > 0;
        } catch (error) {
            console.error('Error checking Telegram init data:', error);
            return false;
        }
    },

    // Get the Telegram WebApp initialization data
    getInitData: function () {
        try {
            if (!window.Telegram || !window.Telegram.WebApp) {
                console.log('Telegram WebApp not available for getInitData');
                return '';
            }

            const initData = window.Telegram.WebApp.initData;
            console.log('Retrieved Telegram init data:', initData ? '[DATA_PRESENT]' : '[NO_DATA]');

            return initData || '';
        } catch (error) {
            console.error('Error getting Telegram init data:', error);
            return '';
        }
    },

    // Initialize Telegram WebApp and return init data (legacy compatibility)
    initializeTelegramWebApp: function () {
        try {
            if (!window.Telegram || !window.Telegram.WebApp) {
                console.log('Telegram WebApp not available for initialization');
                return '';
            }

            // Initialize the WebApp
            const webApp = window.Telegram.WebApp;

            // Configure WebApp settings
            webApp.ready();
            webApp.expand();

            // Set theme
            if (webApp.colorScheme === 'dark') {
                document.documentElement.setAttribute('data-theme', 'dark');
            }

            console.log('Telegram WebApp initialized successfully');
            return webApp.initData || '';
        } catch (error) {
            console.error('Error initializing Telegram WebApp:', error);
            return '';
        }
    },

    // Show Telegram back button
    showBackButton: function () {
        try {
            if (window.Telegram && window.Telegram.WebApp) {
                window.Telegram.WebApp.BackButton.show();
                window.Telegram.WebApp.BackButton.onClick(window.handleBackButton);
                console.log('Telegram back button shown');
            }
        } catch (error) {
            console.error('Error showing Telegram back button:', error);
        }
    },

    // Hide Telegram back button
    hideBackButton: function () {
        try {
            if (window.Telegram && window.Telegram.WebApp) {
                window.Telegram.WebApp.BackButton.hide();
                console.log('Telegram back button hidden');
            }
        } catch (error) {
            console.error('Error hiding Telegram back button:', error);
        }
    },

    // Set back button callback
    setBackButtonCallback: function (callbackFunction) {
        try {
            if (window.Telegram && window.Telegram.WebApp) {
                window.Telegram.WebApp.BackButton.onClick(function () {
                    if (typeof callbackFunction === 'function') {
                        callbackFunction();
                    } else if (typeof window[callbackFunction] === 'function') {
                        window[callbackFunction]();
                    }
                });
                console.log('Telegram back button callback set');
            }
        } catch (error) {
            console.error('Error setting Telegram back button callback:', error);
        }
    },

    // Save data to Telegram cloud storage
    saveToCloud: function (key, value) {
        return new Promise((resolve, reject) => {
            try {
                if (window.Telegram && window.Telegram.WebApp && window.Telegram.WebApp.CloudStorage) {
                    window.Telegram.WebApp.CloudStorage.setItem(key, value, function (error) {
                        if (error) {
                            console.error('Error saving to Telegram cloud:', error);
                            reject(error);
                        } else {
                            console.log('Data saved to Telegram cloud:', key);
                            resolve();
                        }
                    });
                } else {
                    console.log('Telegram cloud storage not available, using localStorage fallback');
                    localStorage.setItem('tg_cloud_' + key, value);
                    resolve();
                }
            } catch (error) {
                console.error('Error in saveToCloud:', error);
                reject(error);
            }
        });
    },

    // Load data from Telegram cloud storage
    loadFromCloud: function (key) {
        return new Promise((resolve, reject) => {
            try {
                if (window.Telegram && window.Telegram.WebApp && window.Telegram.WebApp.CloudStorage) {
                    window.Telegram.WebApp.CloudStorage.getItem(key, function (error, value) {
                        if (error) {
                            console.error('Error loading from Telegram cloud:', error);
                            reject(error);
                        } else {
                            console.log('Data loaded from Telegram cloud:', key);
                            resolve(value || '');
                        }
                    });
                } else {
                    console.log('Telegram cloud storage not available, using localStorage fallback');
                    const value = localStorage.getItem('tg_cloud_' + key) || '';
                    resolve(value);
                }
            } catch (error) {
                console.error('Error in loadFromCloud:', error);
                reject(error);
            }
        });
    },

    // Close Telegram WebApp
    closeWebApp: function () {
        try {
            if (window.Telegram && window.Telegram.WebApp) {
                window.Telegram.WebApp.close();
                console.log('Telegram WebApp closed');
            }
        } catch (error) {
            console.error('Error closing Telegram WebApp:', error);
        }
    },

    // Show Telegram alert
    showAlert: function (message) {
        try {
            if (window.Telegram && window.Telegram.WebApp) {
                window.Telegram.WebApp.showAlert(message);
            } else {
                alert(message);
            }
        } catch (error) {
            console.error('Error showing Telegram alert:', error);
            alert(message);
        }
    },

    // Share to Telegram
    shareToTele: function (url) {
        try {
            if (window.Telegram && window.Telegram.WebApp) {
                const shareUrl = `https://t.me/share/url?url=${encodeURIComponent(url)}`;
                window.Telegram.WebApp.openTelegramLink(shareUrl);
            } else {
                window.open(`https://t.me/share/url?url=${encodeURIComponent(url)}`, '_blank');
            }
        } catch (error) {
            console.error('Error sharing to Telegram:', error);
        }
    },

    // Get user info from Telegram WebApp
    getUserInfo: function () {
        try {
            if (window.Telegram && window.Telegram.WebApp && window.Telegram.WebApp.initDataUnsafe) {
                const user = window.Telegram.WebApp.initDataUnsafe.user;
                return user ? {
                    id: user.id,
                    firstName: user.first_name,
                    lastName: user.last_name,
                    username: user.username,
                    languageCode: user.language_code
                } : null;
            }
            return null;
        } catch (error) {
            console.error('Error getting Telegram user info:', error);
            return null;
        }
    },

    // Check if app is running in Telegram
    isTelegramWebApp: function () {
        return !!(window.Telegram && window.Telegram.WebApp);
    }
};

// Legacy function compatibility (for existing code)
window.initializeTelegramWebApp = window.telegramAuthUtils.initializeTelegramWebApp;
window.showBackButton = window.telegramAuthUtils.showBackButton;
window.hideBackButton = window.telegramAuthUtils.hideBackButton;
window.setBackButtonCallback = window.telegramAuthUtils.setBackButtonCallback;
window.saveToCloud = window.telegramAuthUtils.saveToCloud;
window.loadFromCloud = window.telegramAuthUtils.loadFromCloud;
window.close = window.telegramAuthUtils.closeWebApp;
window.showAlert = window.telegramAuthUtils.showAlert;
window.shareToTele = window.telegramAuthUtils.shareToTele;

// LocalStorage helper functions (legacy compatibility)
window.LSset = function (key, value) {
    try {
        localStorage.setItem(key, value);
        console.log('LocalStorage set:', key);
    } catch (error) {
        console.error('Error setting localStorage:', error);
    }
};

window.LSget = function (key) {
    try {
        const value = localStorage.getItem(key);
        console.log('LocalStorage get:', key, value ? '[VALUE_PRESENT]' : '[NO_VALUE]');
        return value || '';
    } catch (error) {
        console.error('Error getting localStorage:', error);
        return '';
    }
};

window.handleBackButton = function () {
    if (window.history.length > 1) {
        window.history.back();
    } else {
        window.location.href = '/';
    }
};

// Auto-initialize when DOM is ready
document.addEventListener('DOMContentLoaded', function () {
    if (window.telegramAuthUtils.isTelegramWebApp()) {
        console.log('Telegram WebApp detected, initializing...');
        window.telegramAuthUtils.initializeTelegramWebApp();
    } else {
        console.log('Not running in Telegram WebApp');
    }
});

console.log('Telegram authentication utilities loaded');