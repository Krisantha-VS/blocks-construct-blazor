window.blazorDevices = {
    createObserver: function (sentinel, dotnetRef) {
        const observer = new IntersectionObserver(
            (entries) => {
                const first = entries[0];
                if (first && first.isIntersecting) {
                    dotnetRef.invokeMethodAsync('OnSentinelVisible');
                }
            },
            { threshold: 1.0 }
        );

        if (sentinel) {
            observer.observe(sentinel);
        }

        return observer;
    },

    destroyObserver: function (observer) {
        if (observer) {
            observer.disconnect();
        }
    }
};