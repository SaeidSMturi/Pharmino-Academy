        document.addEventListener('DOMContentLoaded', () => {

        // Preloader Logic
        const preloader = document.getElementById('preloader');
        window.addEventListener('load', () => {
        setTimeout(() => {
        preloader.style.opacity = '0';
        preloader.style.visibility = 'hidden';
        }, 400);
        });

        // Offcanvas Mobile Menu Logic
        const mobileMenuButton = document.getElementById('mobile-menu-button');
        const offcanvasMenu = document.getElementById('offcanvas-menu');
        const offcanvasBackdrop = document.getElementById('offcanvas-backdrop');
        const offcanvasCloseButton = document.getElementById('offcanvas-close-button');
        const offcanvasLinks = document.querySelectorAll('.offcanvas-item[href^="#"]'); // Only scroll links

        function openMenu() {
        document.body.classList.add('overflow-hidden');
        offcanvasBackdrop.classList.remove('hidden');
        // Trigger reflow
        void offcanvasBackdrop.offsetWidth;

        offcanvasBackdrop.classList.remove('opacity-0');
        offcanvasMenu.classList.remove('translate-x-full');

        // Add class for staggered item animations
        setTimeout(() => {
        offcanvasMenu.classList.add('menu-open');
        }, 100);
        }

        function closeMenu() {
        document.body.classList.remove('overflow-hidden');
        offcanvasBackdrop.classList.add('opacity-0');
        offcanvasMenu.classList.remove('menu-open');

        setTimeout(() => {
        offcanvasMenu.classList.add('translate-x-full');
        }, 100);

        setTimeout(() => {
        offcanvasBackdrop.classList.add('hidden');
        }, 500);
        }

        if(mobileMenuButton) mobileMenuButton.addEventListener('click', openMenu);
        if(offcanvasCloseButton) offcanvasCloseButton.addEventListener('click', closeMenu);
        if(offcanvasBackdrop) offcanvasBackdrop.addEventListener('click', closeMenu);
        offcanvasLinks.forEach(link => link.addEventListener('click', closeMenu));

        

        // User Dropdown Logic
        const userMenuButton = document.getElementById('user-menu-button');
        const userMenu = document.getElementById('user-menu');
        const userArrow = document.getElementById('user-arrow');

        if (userMenuButton && userMenu) {
        userMenuButton.addEventListener('click', (e) => {
        e.stopPropagation();
        if (userMenu.classList.contains('hidden')) {
        userMenu.classList.remove('hidden');
        if (userArrow) userArrow.classList.add('rotate-180');
        setTimeout(() => {
        userMenu.classList.remove('opacity-0', 'scale-95');
        userMenu.classList.add('opacity-100', 'scale-100');
        }, 10);
        } else {
        if (userArrow) userArrow.classList.remove('rotate-180');
        userMenu.classList.remove('opacity-100', 'scale-100');
        userMenu.classList.add('opacity-0', 'scale-95');
        setTimeout(() => {
        userMenu.classList.add('hidden');
        }, 250);
        }
        });
        }

        window.addEventListener('click', (e) => {
        if (userMenu && !userMenu.classList.contains('hidden')) {
        if (!userMenu.contains(e.target) && !userMenuButton.contains(e.target)) {
        if (userArrow) userArrow.classList.remove('rotate-180');
        userMenu.classList.remove('opacity-100', 'scale-100');
        userMenu.classList.add('opacity-0', 'scale-95');
        setTimeout(() => {
        userMenu.classList.add('hidden');
        }, 250);
        }
        }
        });

        // 3D Tilt Effect for Testimonials
        const tiltCards = document.querySelectorAll('.tilt-card');
        tiltCards.forEach(card => {
        card.addEventListener('mousemove', (e) => {
        const rect = card.getBoundingClientRect();
        const x = e.clientX - rect.left;
        const y = e.clientY - rect.top;

        const centerX = rect.width / 2;
        const centerY = rect.height / 2;

        const rotateX = ((y - centerY) / centerY) * -15;
        const rotateY = ((x - centerX) / centerX) * 15;

        card.style.transform = `perspective(1000px) rotateX(${rotateX}deg) rotateY(${rotateY}deg) scale3d(1.05, 1.05, 1.05)`;
        });

        card.addEventListener('mouseleave', () => {
        card.style.transform = `perspective(1000px) rotateX(0deg) rotateY(0deg) scale3d(1, 1, 1)`;
        card.style.transition = 'transform 0.5s cubic-bezier(0.25, 1, 0.5, 1)';
        });

        card.addEventListener('mouseenter', () => {
        card.style.transition = 'transform 0.1s ease-out';
        });
        });
        });

    tailwind.config = {
        theme: {
            extend: {
                colors: {
                    custom: {
                        // رنگ‌های اصلی سایت (برای جلوگیری از به هم ریختن بقیه المان‌ها)
                        blue: '#4272FF',
                        cyan: '#42EAFF',
                        yellow: '#FFB343',
                        orange: '#FF7E42',
                        dark: '#060913',
                        card: 'rgba(17, 24, 39, 0.7)',
                        snow: '#F8FAFC',
                        
                        // رنگ‌های اختصاصی جدید برای هدر
                        navy: '#123C76',
                        gold: '#E1D788'
                    }
                },
                fontFamily: {
                    sans: ['Inter', 'sans-serif'],
                }
            }
        }
    }
