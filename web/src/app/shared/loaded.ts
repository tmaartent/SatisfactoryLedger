import { Resource } from '@angular/core';

/**
 * What a resource has loaded, or nothing: while it loads, and when it failed to load. Its own value() throws then
 * (Angular 20 on), which leaves the page half drawn: the snapshot picker stayed greyed out after its list failed to
 * load once (step 98). A page reads what it loaded with this, and says why there's nothing with error().
 */
export function loaded<T>(resource: Resource<T>): T | undefined {
  return resource.hasValue() ? resource.value() : undefined;
}
