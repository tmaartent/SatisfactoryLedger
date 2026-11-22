import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

import { TraceCause } from '../../api/models/trace-cause';

/** A finding to explain (ADR-0030): the machines of one building and recipe in a snapshot, and what holds them back. */
export interface ExplanationRequest {
  snapshotId: string;
  building: string;
  recipe: string | null;
  limit: 'starved' | 'outputBlocked';
  /** A model's key; the Assistant's default when left out. */
  model?: string;
}

/** A model the Assistant may use: the key to ask for it by, and the model it runs. */
export interface ChatModel {
  key: string;
  model: string;
}

/** An explanation, the model that wrote it (none for the trace's own note), the facts it stands on, and the numbers that aren't in them. */
export interface Explanation {
  text: string;
  model: string | null;
  facts: { tool: string; arguments: Record<string, unknown>; answer: TraceAnswer };
  unsupportedNumbers: string[];
}

/** A trace as the MCP server's trace_upstream and trace_downstream answer it: in-game names, nulls left out. */
export interface TraceAnswer {
  building: string;
  recipe?: string;
  machines: number;
  traced: number;
  starts: TraceStart[];
  note?: string;
}

/**
 * Where a shortage or blockage starts: the cause, the item, the buildings it starts at, the steps that lead there from
 * the machines asked about and, for a share, who else gets it.
 */
export interface TraceStart {
  cause: TraceCause;
  /** The cause in the ledger's terms, as the MCP server writes it (TraceCauses.cs): "taken by others". */
  causeTerm: string;
  item: string;
  at: TraceBuildings[];
  path: TraceStep[];
  perMinute: number;
  others?: TraceBuildings[];
}

/**
 * Machines of one kind on the way: short of an item (upstream) or unable to get it away (downstream). The machines of
 * one step come one kind at a time, each with the whole step's rate.
 */
export interface TraceStep {
  building: string;
  recipe?: string;
  machines: number;
  ids: string[];
  shortOf?: string;
  cannotLeave?: string;
  perMinute: number;
}

export interface TraceBuildings {
  building: string;
  recipe?: string;
  count: number;
  perMinute?: number;
  ids: string[];
}

/**
 * The Assistant's endpoints, called by hand: they aren't in api/openapi.json, which is the API's contract. The dev
 * server's proxy sends /api/v1/assistant to the Assistant.
 */
@Injectable({ providedIn: 'root' })
export class Assistant {
  private readonly http = inject(HttpClient);

  models(): Promise<ChatModel[]> {
    return firstValueFrom(this.http.get<ChatModel[]>('/api/v1/assistant/models'));
  }

  explain(request: ExplanationRequest): Promise<Explanation> {
    return firstValueFrom(this.http.post<Explanation>('/api/v1/assistant/explanations', request));
  }
}
