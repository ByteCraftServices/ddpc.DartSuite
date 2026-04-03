// DartSuite Push Notification Interop
window.dartSuitePush = {
    registerServiceWorker: async function () {
        if (!('serviceWorker' in navigator) || !('PushManager' in window)) {
            return { supported: false };
        }
        try {
            const reg = await navigator.serviceWorker.register('/service-worker.js');
            await navigator.serviceWorker.ready;
            return { supported: true, registered: true };
        } catch (e) {
            console.error('SW registration failed:', e);
            return { supported: true, registered: false, error: e.message };
        }
    },

    subscribePush: async function (vapidPublicKey) {
        if (!('serviceWorker' in navigator) || !('PushManager' in window)) {
            return null;
        }
        try {
            const reg = await navigator.serviceWorker.ready;
            const existing = await reg.pushManager.getSubscription();
            if (existing) {
                return JSON.parse(JSON.stringify(existing));
            }

            const sub = await reg.pushManager.subscribe({
                userVisibleOnly: true,
                applicationServerKey: this._urlBase64ToUint8Array(vapidPublicKey)
            });
            return JSON.parse(JSON.stringify(sub));
        } catch (e) {
            console.error('Push subscription failed:', e);
            return null;
        }
    },

    unsubscribePush: async function () {
        if (!('serviceWorker' in navigator)) return false;
        try {
            const reg = await navigator.serviceWorker.ready;
            const sub = await reg.pushManager.getSubscription();
            if (sub) {
                await sub.unsubscribe();
            }
            return true;
        } catch (e) {
            console.error('Push unsubscribe failed:', e);
            return false;
        }
    },

    getExistingSubscription: async function () {
        if (!('serviceWorker' in navigator) || !('PushManager' in window)) return null;
        try {
            const reg = await navigator.serviceWorker.ready;
            const sub = await reg.pushManager.getSubscription();
            return sub ? JSON.parse(JSON.stringify(sub)) : null;
        } catch {
            return null;
        }
    },

    isPushSupported: function () {
        return 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window;
    },

    getNotificationPermission: function () {
        return 'Notification' in window ? Notification.permission : 'unsupported';
    },

    requestNotificationPermission: async function () {
        if (!('Notification' in window)) return 'unsupported';
        return await Notification.requestPermission();
    },

    _urlBase64ToUint8Array: function (base64String) {
        const padding = '='.repeat((4 - base64String.length % 4) % 4);
        const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
        const rawData = window.atob(base64);
        const outputArray = new Uint8Array(rawData.length);
        for (let i = 0; i < rawData.length; ++i) {
            outputArray[i] = rawData.charCodeAt(i);
        }
        return outputArray;
    }
};
