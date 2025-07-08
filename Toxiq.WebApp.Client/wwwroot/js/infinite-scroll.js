// wwwroot/js/infinite-scroll.js
// Simple IntersectionObserver - just detects intersection, doesn't handle loading

export function initialize(lastItemIndicator, componentInstance) {
    const options = {
        root: findClosestScrollContainer(lastItemIndicator),
        rootMargin: '50px', // Start loading 50px before the element is visible
        threshold: 0
    };

    const observer = new IntersectionObserver(async (entries) => {
        for (const entry of entries) {
            if (entry.isIntersecting) {
                console.log('Intersection detected, notifying component');

                // Stop observing temporarily to prevent multiple triggers
                observer.unobserve(lastItemIndicator);

                try {
                    // Just notify the component - let it handle the logic
                    await componentInstance.invokeMethodAsync("OnIntersection");
                } catch (error) {
                    console.error('Error notifying component of intersection:', error);
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