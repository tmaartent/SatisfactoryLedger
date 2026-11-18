import * as THREE from 'three';

import { Position } from '../../shared/display';
import { Box } from './area-model';

/** The six sides of a box, as corner numbers (see Box): drawn as two triangles each. */
const SIDES = [[0, 1, 3, 2], [4, 6, 7, 5], [0, 4, 5, 1], [2, 3, 7, 6], [0, 2, 6, 4], [1, 5, 7, 3]];

/** A box as a three.js geometry, each corner placed in the scene by `at`. */
export function boxGeometry(box: Box, at: (p: Position) => THREE.Vector3): THREE.BufferGeometry {
  const corners = box.map(at);
  const positions: number[] = [];
  for (const [a, b, c, d] of SIDES) for (const k of [a, b, c, a, c, d]) positions.push(corners[k].x, corners[k].y, corners[k].z);
  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
  geometry.computeVertexNormals();
  return geometry;
}
