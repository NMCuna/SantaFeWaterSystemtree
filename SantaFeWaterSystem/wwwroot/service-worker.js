self.addEventListener("push", function (event) {
    console.log("📩 Push event received");

    let data = {
        title: "📢 Notification",
        body: "You have a new message.",
        url: "/"
    };

    try {
        if (event.data) {
            const parsed = event.data.json();
            console.log("✅ Push payload:", parsed);

            data.title = parsed.title || data.title;
            data.body = parsed.body || data.body;
            data.url = parsed.url || data.url;
        } else {
            console.warn("⚠️ No data in push event");
        }
    } catch (e) {
        console.error("❌ Failed to parse push event data as JSON:", e);
    }

    const options = {
        body: data.body,
        icon: "https://cdn-icons-png.flaticon.com/512/3135/3135715.png", // ✅ public icon URL
        badge: "https://cdn-icons-png.flaticon.com/512/3135/3135715.png", // ✅ fallback badge
        data: {
            url: data.url
        }
    };

    event.waitUntil(
        self.registration.showNotification(data.title, options)
    );
});

self.addEventListener("notificationclick", function (event) {
    console.log("🖱️ Notification clicked");
    event.notification.close();

    event.waitUntil(
        clients.matchAll({ type: "window", includeUncontrolled: true }).then(clientList => {
            for (const client of clientList) {
                if (client.url === event.notification.data.url && "focus" in client) {
                    console.log("🔁 Focusing existing tab");
                    return client.focus();
                }
            }

            if (clients.openWindow) {
                console.log("🆕 Opening new tab");
                return clients.openWindow(event.notification.data.url);
            }
        })
    );
});
