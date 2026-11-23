import { MachineLimit } from '../api/models/machine-limit';

/**
 * A count picked in the overview of the findings: the machines, or the generators, of one status, or the bottlenecks
 * (both asked 2026-11-23).
 */
export type ShownStatus =
  { of: 'machines' | 'generators'; limit: MachineLimit } | { of: 'bottlenecks' };
