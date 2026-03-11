window.btssAuth = {
    async get(url) {
        const response = await fetch(url, {
            method: "GET",
            credentials: "include",
            headers: {
                "Accept": "application/json"
            }
        });

        const text = await response.text();
        return {
            ok: response.ok,
            status: response.status,
            text: text
        };
    },

    async post(url, body) {
        const response = await fetch(url, {
            method: "POST",
            credentials: "include",
            headers: {
                "Content-Type": "application/json",
                "Accept": "application/json"
            },
            body: JSON.stringify(body)
        });

        const text = await response.text();
        return {
            ok: response.ok,
            status: response.status,
            text: text
        };
    }
};