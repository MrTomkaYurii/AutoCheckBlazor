window.dialogInterop = {
    unlockResize: function (glassId) {
        const glass = document.getElementById(glassId);
        if (!glass) return;
        // Walk up to .mud-dialog and remove overflow / max-height constraints
        let el = glass.parentElement;
        while (el) {
            el.style.overflow = 'visible';
            el.style.maxHeight = 'none';
            if (el.classList.contains('mud-dialog')) break;
            el = el.parentElement;
        }
    }
};
