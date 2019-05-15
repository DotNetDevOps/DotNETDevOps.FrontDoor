
export default {
  install(Vue: Vue.VueConstructor) {
    let app = new Vue({
      data() {
        return {
          isRTL: false
        }
      },
      methods: {
        getDocClasses() {
          return document.body.classList
        },
        enableRTL() {
          this.isRTL = true;
          this.getDocClasses().add('rtl');
          this.getDocClasses().add('menu-on-right');
          this.toggleBootstrapRTL(true);
        },
        disableRTL() {
          this.isRTL = false;
          this.getDocClasses().remove('rtl');
          this.getDocClasses().remove('menu-on-right');
          this.toggleBootstrapRTL(false);
        },
        toggleBootstrapRTL(value: boolean) {
          for (let i=0; i < document.styleSheets.length; i++){
            let styleSheet = document.styleSheets[i];
            let { href } = styleSheet;
            if(href && href.endsWith('bootstrap-rtl.css')){
              styleSheet.disabled = !value;
            }
          }
        }
      }
    });

    Vue.prototype.$rtl = app;
  }
}
