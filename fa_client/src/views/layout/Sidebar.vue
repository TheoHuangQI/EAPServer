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
    props: {
        isCollapsed: {
            type: Boolean,
            required: true
        }
    },
    data() {
        return {
            activeMenu: "数据概览",
            activeSubmenu: "",
            menuItems: [
                {
                    title: "首页",
                    icon: "fas fa-home",
                    isOpen: false,
                },
                {
                    title: "用户管理",
                    icon: "fas fa-users",
                    isOpen: false,
                    children: ["用户列表", "角色权限"],
                },
                {
                    title: "作业信息",
                    icon: "fas fa-align-justify",
                    isOpen: false,
                    children: ["载具管理", "LOT 管理", "Batch 管理"],
                },
                {
                    title: "设备管理",
                    icon: "fas fa-cog",
                    isOpen: false,
                    children: ["设备列表","EAP看板", "作业记录","异常记录"],
                },
                {
                    title: "数据采集",
                    icon: "fas fa-database",
                    isOpen: false,
                    children: ["采集规则","采集历史"],
                },
                {
                    title: "测试管理",
                    icon: "fas fa-briefcase",
                    isOpen: false,
                    children: ["单测试项","复合测试项","测试模版","测试用例","测试结果"],
                },
                {
                    title: "流程控制",
                    icon: "fas fa-sort-amount-asc",
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
                this.$nextTick(() => {
                    switch (this.activeMenu) {
                        case '首页':
                            this.$router.push('/Dashboard');
                            break;
                    }
                });
            }
        },
        handleSubmenuClick(submenu) {
            this.activeSubmenu = submenu;
            this.$nextTick(() => {
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
                    case 'EAP看板':
                        this.$router.push('/EAP');
                        break;
                    case '设备列表':
                        this.$router.push('/EqpList');
                        break;
                    case '异常记录':
                        this.$router.push('/Exception');
                        break;
                    case '作业记录':
                        this.
$router.push('/RunHistory');
                        break;
                    case '采集规则':
                        this.$router.push('/DataRule');
                        break;
                    case '采集历史':
                        this.$router.push('/DataHistory');
                        break;
                    case '单测试项':
                        this.$router.push('/TestItem');
                        break;
                    case '复合测试项':
                        this.$router.push('/TestItemBatch');
                        break;
                    case '测试模版':
                        this.$router.push('/TestTemplate');
                        break;
                    case '测试用例':
                        this.$router.push('/TestCase');
                        break;
                    case '用户列表':
                        this.$router.push('/UserList');
                        break;
                    case '角色权限':
                        this.$router.push('/RolePermission');
                        break;


                }
            });
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
    overflow-y: auto;
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