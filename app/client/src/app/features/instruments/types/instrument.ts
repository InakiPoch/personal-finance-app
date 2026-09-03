import { InstrumentType } from '../../../core/types/instrument-type';

export type Instrument = {
  id: string;
  type: InstrumentType;
  name: string;
  cutoffDate: number | null;
};
