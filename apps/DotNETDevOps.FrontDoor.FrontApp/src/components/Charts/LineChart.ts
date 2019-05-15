import { Line, mixins as chartMixins } from 'vue-chartjs';
import Vue from 'vue'
import Component, { mixins } from 'vue-class-component'
import { Prop } from 'vue-property-decorator'


@Component
export default class LineChart extends mixins(Line, chartMixins.reactiveProp) {

  ctx: CanvasRenderingContext2D | null = null;
  chartId: string | undefined;

  @Prop({ default: () => [1, 0.4, 0],type: Array, validator: val => val.length > 2 }) gradientStops !: [number, number, number];
  @Prop({ default: () => ['rgba(72,72,176,0.2)', 'rgba(72,72,176,0.0)', 'rgba(119,52,169,0)'], type: Array, validator: val => val.length > 2 }) gradientColors !: [string, string, string];
  @Prop(Object) extraOptions !: Object;
 // @Prop({ type: Object, validator: val => { console.log(val); return val !== null; }}) chartData !: Object;

  updateGradients(chartData) {
    if (!chartData) return;
    const ctx = this.ctx || (document.getElementById(this.chartId as string) as HTMLCanvasElement).getContext('2d') as CanvasRenderingContext2D;
    const gradientStroke = ctx.createLinearGradient(0, 230, 0, 50);

    gradientStroke.addColorStop(this.gradientStops[0], this.gradientColors[0]);
    gradientStroke.addColorStop(this.gradientStops[1], this.gradientColors[1]);
    gradientStroke.addColorStop(this.gradientStops[2], this.gradientColors[2]);
    chartData.datasets.forEach(set => {
      set.backgroundColor = gradientStroke;
    });
  }

  mounted() {
     
    this.$watch('chartData', (newVal, oldVal) => {
      this.updateGradients(newVal);
      if (!oldVal) {
        this.renderChart(
          this.chartData,
          this.extraOptions
        );
      }
    }, { immediate: true });
  }

}


//export default {
//  name: 'line-chart',
//  extends: Line,
//  mixins: [mixins.reactiveProp],
//  props: {
//    extraOptions: Object,
//    gradientColors: {
//      type: Array,
//      default: () => ['rgba(72,72,176,0.2)', 'rgba(72,72,176,0.0)', 'rgba(119,52,169,0)'],
//      validator: val => {
//        return val.length > 2;
//      }
//    },
//    gradientStops: {
//      type: Array,
//      default: () => [1, 0.4, 0],
//      validator: val => {
//        return val.length > 2;
//      }
//    }
//  },
//  data() {
//    return {
//      ctx: null
//    };
//  },
//  methods: {
//    updateGradients(chartData) {
//      if(!chartData) return;
//      const ctx = this.ctx || document.getElementById(this.chartId).getContext('2d');
//      const gradientStroke = ctx.createLinearGradient(0, 230, 0, 50);

//      gradientStroke.addColorStop(this.gradientStops[0], this.gradientColors[0]);
//      gradientStroke.addColorStop(this.gradientStops[1], this.gradientColors[1]);
//      gradientStroke.addColorStop(this.gradientStops[2], this.gradientColors[2]);
//      chartData.datasets.forEach(set => {
//        set.backgroundColor = gradientStroke;
//      });
//    }
//  },
//  mounted() {
//    this.$watch('chartData', (newVal, oldVal) => {
//      this.updateGradients(this.chartData);
//      if (!oldVal) {
//        this.renderChart(
//          this.chartData,
//          this.extraOptions
//        );
//      }
//    }, { immediate: true });
//  }
//};
