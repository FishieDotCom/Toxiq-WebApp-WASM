window.infiniteScroll = {
    observer: null,
    mutationObserver: null,
    resizeObserver: null,

    setupInfiniteScroll: function (elementId, scrollBoxId, dotNetObject, threshold = 0.1) {
        const scrollBox = document.getElementById(scrollBoxId);

        if (!scrollBox) {
            console.error(`Scroll box with id "${scrollBoxId}" not found.`);
            return;
        }

        // Ensure scroll box has proper styling
        if (!scrollBox.style.overflow || !scrollBox.style.overflowY) {
            scrollBox.style.overflowY = "auto";
        }

        // Define the function to check and observe
        const observeTarget = () => {
            const target = document.getElementById(elementId);

            if (target) {
                this.startObserving(target, scrollBox, dotNetObject, threshold);
                this.cleanupMutationObserver(); // Stop mutation observer once target is found
            } else {
                console.warn(`Target element "${elementId}" not found. Waiting for DOM changes.`);
            }
        };

        // Set up MutationObserver to detect dynamic addition of the target element
        this.mutationObserver = new MutationObserver(observeTarget);
        this.mutationObserver.observe(document.body, { childList: true, subtree: true });

        // Initial check in case the target is already in the DOM
        observeTarget();

        // Set up ResizeObserver for the scroll box to handle dynamic size changes
        if (typeof ResizeObserver !== "undefined") {
            this.resizeObserver = new ResizeObserver(() => {
                observeTarget();
            });
            this.resizeObserver.observe(scrollBox);
        }
    },

    startObserving: function (target, scrollBox, dotNetObject, threshold) {
        // Disconnect existing observer to prevent duplicates
        if (this.observer) {
            this.observer.disconnect();
        }


        // Create and start IntersectionObserver
        this.observer = new IntersectionObserver(
            (entries) => {
                entries.forEach((entry) => {
                    const isVisible = entry.isIntersecting;
                    if (dotNetObject && dotNetObject.invokeMethodAsync) {
                        dotNetObject.invokeMethodAsync("HandleVisibilityChanged", isVisible);
                    }
                });
            },
            {
                root: scrollBox,   // Scroll box is the root
                threshold: threshold, // Visibility threshold
            }
        );

        //this.observer.observe(target);
        //const rect = target.getBoundingClientRect();
        //console.log(rect);
        //const scrollBoxRect = scrollBox.getBoundingClientRect();

        //console.log(rect.top);
        //console.log(scrollBoxRect.top);
        //console.log(rect.bottom);
        //console.log(scrollBoxRect.bottom);

        //const isInView = rect.top >= scrollBoxRect.top && rect.bottom <= scrollBoxRect.bottom;
        //console.log(isInView);

        this.observer.observe(target);
    },

    cleanupMutationObserver: function () {
        if (this.mutationObserver) {
            this.mutationObserver.disconnect();
            this.mutationObserver = null;
        }
    },

    stopObserving: function () {
        // Clean up IntersectionObserver
        if (this.observer) {
            this.observer.disconnect();
            this.observer = null;
        }

        // Clean up MutationObserver
        this.cleanupMutationObserver();

        // Clean up ResizeObserver
        if (this.resizeObserver) {
            this.resizeObserver.disconnect();
            this.resizeObserver = null;
        }
    },
};