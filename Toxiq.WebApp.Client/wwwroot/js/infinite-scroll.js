// wwwroot/js/infinite-scroll.js
// Simple IntersectionObserver-based infinite scroll

export function initialize(lastItemIndicator, componentInstance) {
    const options = {
        root: findClosestScrollContainer(lastItemIndicator),
        rootMargin: '50px', // Start loading 50px before the element is visible
        threshold: 0
    };

    const observer = new IntersectionObserver(async (entries) => {
        for (const entry of entries) {
            if (entry.isIntersecting) {
                // Stop observing temporarily to prevent multiple triggers
                observer.unobserve(lastItemIndicator);

                try {
                    await componentInstance.invokeMethodAsync("LoadMoreItems");
                } catch (error) {
                    console.error('Error loading more items:', error);
                    // Re-observe even if there's an error
                    observer.observe(lastItemIndicator);
                }
            }
        }
    }, options);

    observer.observe(lastItemIndicator);

    return {
        dispose: () => {
            observer.disconnect();
        },
        onNewItems: () => {
            // Re-observe the sentinel element after new items are loaded
            observer.unobserve(lastItemIndicator);
            observer.observe(lastItemIndicator);
        }
    };
}

function findClosestScrollContainer(element) {
    let parent = element.parentElement;

    while (parent) {
        const style = window.getComputedStyle(parent);
        const overflow = style.overflow + style.overflowY + style.overflowX;

        if (overflow.includes('auto') || overflow.includes('scroll')) {
            return parent;
        }

        parent = parent.parentElement;
    }

    // If no scroll container found, use the viewport (null)
    return null;
}