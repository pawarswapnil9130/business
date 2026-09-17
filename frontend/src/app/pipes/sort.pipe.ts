import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'sort'
})
export class SortPipe implements PipeTransform {
  transform(items: any[], field: string, descending: boolean = false): any[] {
    if (!items || !field) {
      return items;
    }

    return items.slice().sort((a, b) => {
      let valA = this.getNestedValue(a, field);
      let valB = this.getNestedValue(b, field);

      if (valA === null || valA === undefined) valA = '';
      if (valB === null || valB === undefined) valB = '';

      if (typeof valA === 'string' && typeof valB === 'string') {
        valA = valA.toLowerCase();
        valB = valB.toLowerCase();
      }

      if (valA < valB) {
        return descending ? 1 : -1;
      }
      if (valA > valB) {
        return descending ? -1 : 1;
      }
      return 0;
    });
  }

  private getNestedValue(obj: any, path: string): any {
    if (!path) return undefined;
    const keys = path.split('.');
    let value = obj;
    for (const key of keys) {
      if (value === null || value === undefined) return undefined;
      value = value[key];
    }
    return value;
  }
}
