// Smart auto-scroll for chat using MutationObserver + ResizeObserver.
// Reacts to actual DOM changes instead of polling.
// Only scrolls when user is near the bottom of the container.

window.ChatScroll = (function () {
    'use strict';

    let containerId = null;
    let enabled = false;
    let mutationObserver = null;
    let resizeObserver = null;
    let rafId = null;

    const SCROLL_THRESHOLD = 50;

    function getContainer() {
        return document.getElementById(containerId);
    }

    function isNearBottom() {
        const el = getContainer();
        if (!el) return true;
        return el.scrollHeight - el.scrollTop - el.clientHeight < SCROLL_THRESHOLD;
    }

    function scrollNow(smooth) {
        const el = getContainer();
        if (!el) return;
        el.scrollTo({
            top: el.scrollHeight,
            behavior: smooth ? 'smooth' : 'instant'
        });
    }

    function onContentChanged() {
        if (!enabled) return;
        if (!isNearBottom()) return;

        // Debounce via requestAnimationFrame - at most once per frame
        if (rafId) cancelAnimationFrame(rafId);
        rafId = requestAnimationFrame(function () {
            scrollNow(true);
            rafId = null;
        });
    }

    return {
        /**
         * Initialises the scroll observer on the given container element.
         * Automatically cleans up any previous observer instance.
         */
        init: function (id) {
            this.destroy();

            containerId = id;
            const container = getContainer();
            if (!container) return;

            // React to new/removed nodes and text changes inside the container
            mutationObserver = new MutationObserver(onContentChanged);
            mutationObserver.observe(container, {
                childList: true,
                subtree: true,
                characterData: true
            });

            // React to container size changes (content growing/shrinking)
            resizeObserver = new ResizeObserver(onContentChanged);
            resizeObserver.observe(container);
        },

        /** Enables auto-scroll. Scrolls to bottom immediately. */
        enable: function () {
            enabled = true;
            scrollNow(false);
        },

        /** Disables auto-scroll. */
        disable: function () {
            enabled = false;
        },

        /** Scrolls to bottom instantly or smoothly. */
        scrollToBottom: function (smooth) {
            scrollNow(smooth);
        },

        /** Returns true if the user is within threshold of the bottom. */
        isNearBottom: function () {
            return isNearBottom();
        },

        /** Tears down all observers and resets state. */
        destroy: function () {
            enabled = false;
            if (rafId) {
                cancelAnimationFrame(rafId);
                rafId = null;
            }
            if (mutationObserver) {
                mutationObserver.disconnect();
                mutationObserver = null;
            }
            if (resizeObserver) {
                resizeObserver.disconnect();
                resizeObserver = null;
            }
            containerId = null;
        }
    };
})();
