// Toxiq.WebApp.Client/wwwroot/js/createpost-utils.js

window.toxiqCreatePost = {
    // Focus on text area (matches mobile app auto-focus behavior)
    focusElement: (element) => {
        if (element) {
            element.focus();
            // Position cursor at end
            const length = element.value.length;
            element.setSelectionRange(length, length);
        }
    },

    // Auto-resize textarea as user types (similar to mobile app behavior)
    autoResizeTextarea: (element) => {
        if (!element) return;

        element.style.height = 'auto';
        element.style.height = Math.min(element.scrollHeight, 300) + 'px';
    },

    // Handle keyboard shortcuts
    setupKeyboardShortcuts: (element, publishCallback) => {
        if (!element) return;

        element.addEventListener('keydown', (e) => {
            // Ctrl/Cmd + Enter to publish
            if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') {
                e.preventDefault();
                publishCallback.invokeMethodAsync('PublishPost');
            }

            // Auto-resize on input
            setTimeout(() => {
                window.toxiqCreatePost.autoResizeTextarea(element);
            }, 0);
        });

        element.addEventListener('input', () => {
            window.toxiqCreatePost.autoResizeTextarea(element);
        });
    },

    // Prevent accidental navigation when content exists
    setupUnloadProtection: (hasContent) => {
        if (hasContent) {
            window.addEventListener('beforeunload', (e) => {
                e.preventDefault();
                e.returnValue = '';
                return 'You have unsaved changes. Are you sure you want to leave?';
            });
        } else {
            window.removeEventListener('beforeunload', window.toxiqCreatePost.unloadHandler);
        }
    },

    // Store the unload handler reference for cleanup
    unloadHandler: null,

    // Initialize page (called when page loads)
    initializePage: (textAreaElement, publishCallback) => {
        // Focus on text area
        setTimeout(() => {
            window.toxiqCreatePost.focusElement(textAreaElement);
        }, 100);

        // Setup keyboard shortcuts
        window.toxiqCreatePost.setupKeyboardShortcuts(textAreaElement, publishCallback);

        // Setup auto-resize
        window.toxiqCreatePost.autoResizeTextarea(textAreaElement);
    },

    // Cleanup when leaving page
    cleanup: () => {
        if (window.toxiqCreatePost.unloadHandler) {
            window.removeEventListener('beforeunload', window.toxiqCreatePost.unloadHandler);
        }
    },

    // Show toast notification (similar to mobile app Toast.Make)
    showToast: (message, type = 'info', duration = 3000) => {
        // Remove existing toasts
        const existingToasts = document.querySelectorAll('.toxiq-toast');
        existingToasts.forEach(toast => toast.remove());

        // Create toast element
        const toast = document.createElement('div');
        toast.className = `toxiq-toast toxiq-toast-${type}`;
        toast.textContent = message;

        // Style the toast
        Object.assign(toast.style, {
            position: 'fixed',
            top: '20px',
            left: '50%',
            transform: 'translateX(-50%)',
            backgroundColor: type === 'error' ? '#ef4444' : type === 'success' ? '#10b981' : '#3b82f6',
            color: 'white',
            padding: '12px 24px',
            borderRadius: '8px',
            fontSize: '14px',
            fontWeight: '500',
            zIndex: '10000',
            boxShadow: '0 4px 12px rgba(0, 0, 0, 0.15)',
            opacity: '0',
            transition: 'opacity 0.3s ease'
        });

        // Add to page
        document.body.appendChild(toast);

        // Animate in
        setTimeout(() => {
            toast.style.opacity = '1';
        }, 10);

        // Auto remove
        setTimeout(() => {
            toast.style.opacity = '0';
            setTimeout(() => {
                if (toast.parentNode) {
                    toast.parentNode.removeChild(toast);
                }
            }, 300);
        }, duration);
    },

    // Validate post content (matches mobile app validation)
    validatePost: (content, maxLength = 512) => {
        if (!content || content.trim().length === 0) {
            return { isValid: false, error: 'Post content cannot be empty' };
        }

        if (content.length > maxLength) {
            return { isValid: false, error: `Post content exceeds maximum length of ${maxLength} characters` };
        }

        return { isValid: true, error: null };
    },

    // Get character count with formatting
    getCharacterCountInfo: (content, maxLength = 512) => {
        const length = content ? content.length : 0;
        const remaining = maxLength - length;
        const percentage = (length / maxLength) * 100;

        return {
            current: length,
            max: maxLength,
            remaining: remaining,
            percentage: percentage,
            isWarning: percentage > 80,
            isError: percentage > 100
        };
    },

    // Debounce function for performance
    debounce: (func, wait) => {
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
};

// Global focus function for Blazor interop
window.focusElement = (element) => {
    window.toxiqCreatePost.focusElement(element);
};

// Global alert override for better UX
window.alert = (message) => {
    window.toxiqCreatePost.showToast(message, 'info', 4000);
};