<template>
    <div>
        <aside :class="['sidebar', { 'sidebar--collapsed': isCollapsed }]">
            <nav class="sidebar__nav">
                <div v-for="(item, index) in menuItems" :key="index" class="sidebar__item">
                    <button @click="handleMenuClick(item)" :class="[
                        'sidebar__btn',
                        {
                            'sidebar__btn--centered': isCollapsed,
                            'sidebar__btn--active':
                                activeMenu === item.title && !item.children,
                            'sidebar__btn--open': item.isOpen,
                        },
                    ]">
                        <i :class="[item.icon]"></i>
                        <span v-if="!isCollapsed" class="sidebar__title">{{
                            item.title
                        }}</span>
                        <i v-if="!isCollapsed && item.children" :class="[
                            'fas fa-chevron-right sidebar__arrow',
                            { 'icon-rotate': item.isOpen },
                        ]"></i>
                    </button>
                    <transition name="slide">
                        <div v-if="item.children && !isCollapsed && item.isOpen" class="sidebar__submenu">
                            <button v-for="(child, childIndex) in item.children" :key="childIndex"
                                @click="handleSubmenuClick(child)" :class="[
                                    'sidebar__subbtn',
                                    { 'sidebar__subbtn--active': activeSubmenu === child },
                                ]">
                                <div class="sidebar__dot"></div>
                                {{ child }}
                            </button>
                        </div>
                    </transition>
                </div>
            </nav>
        </aside>
    </div>
</template>
<script>
export default {
    data() {
        return {
            activeMenu: "数据概览",
            activeSubmenu: "",
            isCollapsed: false,
            menuItems: [
                {
                    title: "首页",
                    icon: "fas fa-chart-line",
                    isOpen: false,
                },
                {
                    title: "用户管理",
                    icon: "fas fa-users",
                    isOpen: false,
                    children: ["用户列表", "角色权限"],
                },
                {
                    title: "批次管理",
                    icon: "fas fa-file-alt",
                    isOpen: false,
                    children: ["载具管理", "LOT 管理", "Batch 管理"],
                },
                {
                    title: "作业管理",
                    icon: "fas fa-cog",
                    isOpen: false,
                    children: ["EAP看板", "历史记录"],
                },
                {
                    title: "测试用例",
                    icon: "fas fa-file-alt",
                    isOpen: false,
                    children: ["测试用例库", "测试结果"],
                },
                {
                    title: "流程控制",
                    icon: "fas fa-file-alt",
                    isOpen: false,
                    children: ["控制列表"],
                },
            ]
        }
    },
    methods: {
        handleMenuClick(item) {
            if (item.children) {
                item.isOpen = !item.isOpen;
            } else {
                this.activeMenu = item.title;
                this.activeSubmenu = "";
                switch (this.activeMenu) {
                    case '首页':
                        this.$router.push('/Dashboard');
                        break;
                }
            }
        },
        handleSubmenuClick(submenu) {
            this.activeSubmenu = submenu;
            switch (submenu) {

                case 'LOT 管理':
                    this.$router.push('/LOT');
                    break;
                case '载具管理':
                    this.$router.push('/Carrier');
                    break;
                case 'Batch 管理':
                    this.$router.push('/Batch');
                    break;
                case '控制列表':
                    this.$router.push('/Flow');
                    break;
            }
        },
    }
}
</script>
<style>
.sidebar {
    position: fixed;
    left: 0;
    top: 64px;
    bottom: 0;
    width: 256px;
    background-color: white;
    border-right: 1px solid #ddd;
    transition: all 0.3s ease;
    z-index: 40;
}

.sidebar--collapsed {
    width: 80px;
}

.sidebar__nav {
    padding: 12px 0;
}

.sidebar__item {
    margin: 4px 12px;
}

.sidebar__btn {
    width: 100%;
    padding: 12px 16px;
    display: flex;
    align-items: center;
    gap: 12px;
    transition: all 0.3s ease;
    border-radius: 8px;
    color: #4b5563;
    border: none;
    background-color: white;
}

.sidebar__title {
    flex: 1;
    text-align: left;
    font-weight: 500;
}

.sidebar__btn:hover {
    background-color: #f3f4f6;
    color: #2563eb;
}

.sidebar__btn--centered {
    justify-content: center;
}

.sidebar__btn--active {
    background-color: #eff6ff;
    color: #2563eb;
}

.sidebar__btn--open {
    color: #2563eb;
}

.sidebar__btn i {
    font-size: 1.125rem;
    min-width: 20px;
    text-align: center;
}

.sidebar__arrow {
    font-size: 0.875rem;
    transition: transform 0.3s ease;
    color: #9ca3af;
}

.sidebar__submenu {
    margin: 4px 0;
    padding: 4px 0;
}

.sidebar__subbtn {
    width: 100%;
    padding: 10px 16px 10px 48px;
    text-align: left;
    color: #4b5563;
    position: relative;
    transition: all 0.3s ease;
    display: flex;
    align-items: center;
    font-size: 0.875rem;
    border: none;
    background-color: white;
}

.sidebar__dot {
    width: 5px;
    height: 5px;
    border-radius: 50%;
    background-color: #9ca3af;
    position: absolute;
    left: 32px;
    transition: all 0.3s ease;
}

.sidebar__subbtn:hover {
    color: #2563eb;
    background-color: #f3f4f6;
}

.sidebar__subbtn:hover .sidebar__dot {
    background-color: #2563eb;
}

.sidebar__subbtn--active {
    color: #2563eb;
    background-color: #eff6ff;
}

.sidebar__subbtn--active .sidebar__dot {
    background-color: #2563eb;
}
</style>