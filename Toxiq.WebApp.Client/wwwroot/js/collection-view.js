// wwwroot/js/collection-view.js
// CollectionView JavaScript Interop Module

export function setupInfiniteScroll(triggerElement, dotNetRef, methodName) {
    if (!triggerElement || !dotNetRef) {
        console.warn('setupInfiniteScroll: Missing required parameters');
        return null;
    }

    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                // Debounce the callback to prevent multiple rapid calls
                debounce(() => {
                    dotNetRef.invokeMethodAsync(methodName);
                }, 100)();
            }
        });
    }, {
        rootMargin: '100px', // Start loading before the element is fully visible
        threshold: 0.1
    });

    observer.observe(triggerElement);
    return observer;
}

export function setupPullToRefresh(containerElement, dotNetRef, methodName) {
    if (!containerElement || !dotNetRef) {
        console.warn('setupPullToRefresh: Missing required parameters');
        return;
    }

    let startY = 0;
    let currentY = 0;
    let pullDistance = 0;
    let isPulling = false;
    let isRefreshing = false;

    // Touch events for mobile
    containerElement.addEventListener('touchstart', handleTouchStart, { passive: false });
    containerElement.addEventListener('touchmove', handleTouchMove, { passive: false });
    containerElement.addEventListener('touchend', handleTouchEnd, { passive: false });

    // Mouse events for desktop testing
    containerElement.addEventListener('mousedown', handleMouseStart);
    containerElement.addEventListener('mousemove', handleMouseMove);
    containerElement.addEventListener('mouseup', handleMouseEnd);

    function handleTouchStart(e) {
        if (containerElement.scrollTop === 0 && !isRefreshing) {
            startY = e.touches[0].clientY;
            isPulling = true;
        }
    }

    function handleTouchMove(e) {
        if (!isPulling || isRefreshing) return;

        currentY = e.touches[0].clientY;
        pullDistance = currentY - startY;

        if (pullDistance > 0) {
            // Prevent default scrolling when pulling down
            e.preventDefault();

            // Apply resistance to the pull
            const resistance = Math.min(pullDistance * 0.5, 100);
            dotNetRef.invokeMethodAsync(methodName, resistance);

            // Trigger refresh if pulled far enough
            if (pullDistance > 80 && !isRefreshing) {
                isRefreshing = true;
                triggerHapticFeedback();
            }
        }
    }

    function handleTouchEnd(e) {
        if (isPulling) {
            isPulling = false;

            if (isRefreshing) {
                // Let the component handle the refresh animation
                setTimeout(() => {
                    isRefreshing = false;
                }, 1500);
            } else {
                // Reset pull indicator
                dotNetRef.invokeMethodAsync(methodName, 0);
            }
        }
    }

    function handleMouseStart(e) {
        if (containerElement.scrollTop === 0 && !isRefreshing) {
            startY = e.clientY;
            isPulling = true;
        }
    }

    function handleMouseMove(e) {
        if (!isPulling || isRefreshing) return;

        currentY = e.clientY;
        pullDistance = currentY - startY;

        if (pullDistance > 0) {
            e.preventDefault();
            const resistance = Math.min(pullDistance * 0.3, 100);
            dotNetRef.invokeMethodAsync(methodName, resistance);

            if (pullDistance > 120 && !isRefreshing) {
                isRefreshing = true;
            }
        }
    }

    function handleMouseEnd(e) {
        if (isPulling) {
            isPulling = false;

            if (isRefreshing) {
                setTimeout(() => {
                    isRefreshing = false;
                }, 1500);
            } else {
                dotNetRef.invokeMethodAsync(methodName, 0);
            }
        }
    }
}

export function getScrollInfo(element) {
    if (!element) {
        return {
            scrollTop: 0,
            scrollHeight: 0,
            clientHeight: 0
        };
    }

    return {
        scrollTop: element.scrollTop,
        scrollHeight: element.scrollHeight,
        clientHeight: element.clientHeight
    };
}

export function scrollToTop(element, smooth = true) {
    if (!element) return;

    element.scrollTo({
        top: 0,
        behavior: smooth ? 'smooth' : 'instant'
    });
}

export function scrollTo(element, scrollTop, smooth = true) {
    if (!element) return;

    element.scrollTo({
        top: scrollTop,
        behavior: smooth ? 'smooth' : 'instant'
    });
}

export function observeElementSize(element, dotNetRef, methodName) {
    if (!element || !dotNetRef || !window.ResizeObserver) {
        return null;
    }

    const observer = new ResizeObserver(entries => {
        for (let entry of entries) {
            const { width, height } = entry.contentRect;
            dotNetRef.invokeMethodAsync(methodName, width, height);
        }
    });

    observer.observe(element);
    return observer;
}

export function measureItemHeight(element) {
    if (!element) return 0;

    const rect = element.getBoundingClientRect();
    const style = window.getComputedStyle(element);
    const margin = parseFloat(style.marginTop) + parseFloat(style.marginBottom);

    return rect.height + margin;
}

export function enableSmoothScrolling(element) {
    if (!element) return;

    element.style.scrollBehavior = 'smooth';
    element.style.webkitOverflowScrolling = 'touch';
    element.style.overscrollBehavior = 'contain';
}

export function disableSmoothScrolling(element) {
    if (!element) return;

    element.style.scrollBehavior = 'auto';
}

// Performance optimization for large lists
export function requestIdleCallback(callback) {
    if (window.requestIdleCallback) {
        return window.requestIdleCallback(callback);
    } else {
        return setTimeout(callback, 1);
    }
}

export function cancelIdleCallback(id) {
    if (window.cancelIdleCallback) {
        window.cancelIdleCallback(id);
    } else {
        clearTimeout(id);
    }
}

// Utility functions
function debounce(func, wait) {
    let timeout;
    return function executedFunction(...args) {
        const later = () => {
            clearTimeout(timeout);
            func(...args);
        };
        clearTimeout(timeout);
        timeout = setTimeout(later, wait);
    };
}

function throttle(func, limit) {
    let inThrottle;
    return function (...args) {
        if (!inThrottle) {
            func.apply(this, args);
            inThrottle = true;
            setTimeout(() => inThrottle = false, limit);
        }
    };
}

function triggerHapticFeedback() {
    if (navigator.vibrate) {
        navigator.vibrate(50); // Short vibration
    }

    // For iOS Safari
    if (window.AudioContext || window.webkitAudioContext) {
        try {
            const audioContext = new (window.AudioContext || window.webkitAudioContext)();
            const oscillator = audioContext.createOscillator();
            const gainNode = audioContext.createGain();

            oscillator.connect(gainNode);
            gainNode.connect(audioContext.destination);

            oscillator.frequency.setValueAtTime(1000, audioContext.currentTime);
            gainNode.gain.setValueAtTime(0.1, audioContext.currentTime);
            gainNode.gain.exponentialRampToValueAtTime(0.01, audioContext.currentTime + 0.1);

            oscillator.start(audioContext.currentTime);
            oscillator.stop(audioContext.currentTime + 0.1);
        } catch (e) {
            // Ignore audio context errors
        }
    }
}

// Virtual scrolling optimization
export function calculateVisibleRange(scrollTop, containerHeight, itemHeight, totalItems, bufferSize = 5) {
    const startIndex = Math.max(0, Math.floor(scrollTop / itemHeight) - bufferSize);
    const visibleCount = Math.ceil(containerHeight / itemHeight) + (bufferSize * 2);
    const endIndex = Math.min(totalItems - 1, startIndex + visibleCount);

    return {
        startIndex,
        endIndex,
        visibleCount: endIndex - startIndex + 1
    };
}

export function getItemPosition(index, itemHeight) {
    return {
        top: index * itemHeight,
        height: itemHeight
    };
}