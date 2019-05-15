import Sidebar from "./SideBar.vue";
import SidebarLink from "./SidebarLink.vue";

const SidebarStore = {
  showSidebar: false,
  sidebarLinks: [],
  displaySidebar(value: boolean) {
    this.showSidebar = value;
  }
};

const SidebarPlugin = {
  install(Vue: Vue.VueConstructor) {
    let app = new Vue({
      data: {
        sidebarStore: SidebarStore
      }
    });

    Vue.prototype.$sidebar = app.sidebarStore;
    Vue.component("side-bar", Sidebar);
    Vue.component("sidebar-link", SidebarLink);
  }
};

export default SidebarPlugin;
