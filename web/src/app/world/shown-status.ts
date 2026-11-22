import { MachineLimit } from '../api/models/machine-limit';

/** A count picked in the overview of the findings: the machines, or the generators, of one status (asked 2026-11-23). */
export interface ShownStatus {
  of: 'machines' | 'generators';
  limit: MachineLimit;
}
