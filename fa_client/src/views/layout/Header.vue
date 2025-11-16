<template>
    <div class="header">
        <div class="header__container">
            <div class="header__left">
                <button @click="toggleSidebar" class="rounded-btn btn-icon">
                    <i class="fas fa-bars"></i>
                </button>
                <div class="header__logo">
                    <span>FA测试平台</span>
                </div>
            </div>
            <div class="header__right">
                <!-- <button class="rounded-btn btn-notification">
                    <i class="fas fa-bell"></i>
                    <span class="notification-badge">3</span>
                </button> -->
                <div class="user-info">
                    <span>liyb</span>
                </div>
                <button class="rounded-btn btn-logout">
                    <i class="fas fa-sign-out-alt"></i>退出
                </button>
            </div>
        </div>
    </div>
</template>
<script>
export default {
    data() {
        return {
            isCollapsed: false,
            avatarUrl: "@assets/logo.png",
            logoUrl:
                "https://ai-public.mastergo.com/ai/img_res/02469016513ab98d8c035dc0852d545d.jpg",
        }
    },
    methods: {
        toggleSidebar() {
            this.isCollapsed = !this.isCollapsed;
        },
    }
}
</script>
<style>
.rounded-btn {
    border-radius: 8px;
    border: none;
    padding: 8px;
    cursor: pointer;
    background: transparent;
}

.header {
    background-color: #fff;
    box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
    position: fixed;
    top: 0;
    left: 0;
    right: 0;
    height: 64px;
    z-index: 50;
}

.header__container {
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 0 24px;
    height: 100%;
}

.header__left {
    display: flex;
    align-items: center;
    gap: 16px;
}

.header__logo {
    display: flex;
    align-items: center;
    gap: 8px;
}

.header__logo img {
    height: 32px;
    width: 32px;
    object-fit: cover;
    border-radius: 8px;
}

.header__logo span {
    font-size: 1.25rem;
    font-weight: 600;
    color: #1f2937;
}

.header__right {
    display: flex;
    align-items: center;
    gap: 24px;
}

.btn-icon {
    color: #4b5563;
}

.btn-icon:hover {
    color: #111827;
}

.btn-notification {
    position: relative;
}

.notification-badge {
    position: absolute;
    top: -4px;
    right: -4px;
    background-color: #ef4444;
    color: white;
    font-size: 0.75rem;
    width: 16px;
    height: 16px;
    border-radius: 50%;
    display: flex;
    align-items: center;
    justify-content: center;
}

.user-info {
    display: flex;
    align-items: center;
    gap: 12px;
}

.user-info img {
    width: 32px;
    height: 32px;
    border-radius: 50%;
    object-fit: cover;
}

.user-info span {
    color: #4b5563;
}

.btn-logout {
    background-color: #f3f4f6;
    padding: 8px 16px;
    color: #4b5563;
}

.btn-logout:hover {
    background-color: #e5e7eb;
}
</style>