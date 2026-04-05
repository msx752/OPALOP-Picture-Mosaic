window.opalop = {
    registerLazyLoad: function (dotNetRef, selector) {
        const observer = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    const id = entry.target.dataset.photoId;
                    if (id) dotNetRef.invokeMethodAsync('OnPhotoVisible', id);
                    observer.unobserve(entry.target);
                }
            });
        }, { rootMargin: '200px' });
        document.querySelectorAll(selector).forEach(el => observer.observe(el));
    },
    scrollToElement: function (id) {
        document.getElementById(id)?.scrollIntoView({ behavior: 'smooth' });
    }
};
