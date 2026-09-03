import { InstrumentType } from './instrument-type';

export type RegisteredInstrument = {
  id: string;
  type: InstrumentType;
  name: string;
  cutoffDate?: number;
};
