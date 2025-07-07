// Toxiq.WebApp.Client/wwwroot/js/indexeddb-utils.js
// IndexedDB utility functions for Toxiq web app
// Provides persistent storage similar to mobile app's MonkeyCache

window.toxiqIndexedDb = {
    dbName: 'ToxiqWebApp',
    storeName: 'ToxiqStore',
    version: 1,
    db: null,

    // Initialize IndexedDB connection
    init: async function () {
        return new Promise((resolve, reject) => {
            if (this.db) {
                resolve(this.db);
                return;
            }

            const request = indexedDB.open(this.dbName, this.version);

            request.onerror = () => {
                console.error('Error opening IndexedDB:', request.error);
                reject(request.error);
            };

            request.onsuccess = () => {
                this.db = request.result;
                resolve(this.db);
            };

            request.onupgradeneeded = (event) => {
                const db = event.target.result;
                if (!db.objectStoreNames.contains(this.storeName)) {
                    const store = db.createObjectStore(this.storeName, { keyPath: 'key' });
                    store.createIndex('timestamp', 'timestamp', { unique: false });
                }
            };
        });
    },

    // Store item in IndexedDB
    setItem: async function (key, value) {
        try {
            await this.init();

            const transaction = this.db.transaction([this.storeName], 'readwrite');
            const store = transaction.objectStore(this.storeName);

            const item = {
                key: key,
                value: value,
                timestamp: Date.now()
            };

            return new Promise((resolve, reject) => {
                const request = store.put(item);

                request.onsuccess = () => resolve(request.result);
                request.onerror = () => reject(request.error);
            });
        } catch (error) {
            console.error('Error setting item in IndexedDB:', error);
            throw error;
        }
    },

    // Get item from IndexedDB
    getItem: async function (key) {
        try {
            await this.init();

            const transaction = this.db.transaction([this.storeName], 'readonly');
            const store = transaction.objectStore(this.storeName);

            return new Promise((resolve, reject) => {
                const request = store.get(key);

                request.onsuccess = () => {
                    const result = request.result;
                    resolve(result ? result.value : null);
                };

                request.onerror = () => reject(request.error);
            });
        } catch (error) {
            console.error('Error getting item from IndexedDB:', error);
            return null;
        }
    },

    // Remove item from IndexedDB
    removeItem: async function (key) {
        try {
            await this.init();

            const transaction = this.db.transaction([this.storeName], 'readwrite');
            const store = transaction.objectStore(this.storeName);

            return new Promise((resolve, reject) => {
                const request = store.delete(key);

                request.onsuccess = () => resolve();
                request.onerror = () => reject(request.error);
            });
        } catch (error) {
            console.error('Error removing item from IndexedDB:', error);
            throw error;
        }
    },

    // Check if key exists in IndexedDB
    containsKey: async function (key) {
        try {
            await this.init();

            const transaction = this.db.transaction([this.storeName], 'readonly');
            const store = transaction.objectStore(this.storeName);

            return new Promise((resolve, reject) => {
                const request = store.get(key);

                request.onsuccess = () => {
                    resolve(request.result !== undefined);
                };

                request.onerror = () => reject(request.error);
            });
        } catch (error) {
            console.error('Error checking key in IndexedDB:', error);
            return false;
        }
    },

    // Clear all items from IndexedDB
    clear: async function () {
        try {
            await this.init();

            const transaction = this.db.transaction([this.storeName], 'readwrite');
            const store = transaction.objectStore(this.storeName);

            return new Promise((resolve, reject) => {
                const request = store.clear();

                request.onsuccess = () => resolve();
                request.onerror = () => reject(request.error);
            });
        } catch (error) {
            console.error('Error clearing IndexedDB:', error);
            throw error;
        }
    },

    // Get all items from IndexedDB (for debugging)
    getAllItems: async function () {
        try {
            await this.init();

            const transaction = this.db.transaction([this.storeName], 'readonly');
            const store = transaction.objectStore(this.storeName);

            return new Promise((resolve, reject) => {
                const request = store.getAll();

                request.onsuccess = () => resolve(request.result);
                request.onerror = () => reject(request.error);
            });
        } catch (error) {
            console.error('Error getting all items from IndexedDB:', error);
            return [];
        }
    },

    // Clean up old items (items older than specified days)
    cleanupOldItems: async function (daysToKeep = 30) {
        try {
            await this.init();

            const cutoffTime = Date.now() - (daysToKeep * 24 * 60 * 60 * 1000);
            const transaction = this.db.transaction([this.storeName], 'readwrite');
            const store = transaction.objectStore(this.storeName);
            const index = store.index('timestamp');

            const range = IDBKeyRange.upperBound(cutoffTime);

            return new Promise((resolve, reject) => {
                const request = index.openCursor(range);
                let deletedCount = 0;

                request.onsuccess = (event) => {
                    const cursor = event.target.result;
                    if (cursor) {
                        cursor.delete();
                        deletedCount++;
                        cursor.continue();
                    } else {
                        console.log(`Cleaned up ${deletedCount} old items from IndexedDB`);
                        resolve(deletedCount);
                    }
                };

                request.onerror = () => reject(request.error);
            });
        } catch (error) {
            console.error('Error cleaning up IndexedDB:', error);
            return 0;
        }
    }
};

// Initialize IndexedDB when script loads
document.addEventListener('DOMContentLoaded', function () {
    window.toxiqIndexedDb.init().catch(console.error);
});

// Cleanup old items periodically (once per day)
setInterval(() => {
    window.toxiqIndexedDb.cleanupOldItems(30).catch(console.error);
}, 24 * 60 * 60 * 1000);