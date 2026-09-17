import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'searchFilter'
})
export class SearchFilterPipe implements PipeTransform {
  transform(items: any[], searchText: string): any[] {
    if (!items) return [];
    if (!searchText) return items;

    searchText = searchText.toLowerCase();

    return items.filter(item => {
      // Recursively search through object properties
      return this.searchInObj(item, searchText);
    });
  }

  private searchInObj(obj: any, searchText: string): boolean {
    if (!obj) return false;
    for (const key in obj) {
      if (Object.prototype.hasOwnProperty.call(obj, key)) {
        const val = obj[key];
        if (val !== null && val !== undefined) {
          if (typeof val === 'string' && val.toLowerCase().includes(searchText)) {
            return true;
          } else if (typeof val === 'number' && val.toString().includes(searchText)) {
            return true;
          } else if (typeof val === 'object' && !Array.isArray(val) && !(val instanceof Date)) {
            if (this.searchInObj(val, searchText)) {
              return true;
            }
          }
        }
      }
    }
    return false;
  }
}
