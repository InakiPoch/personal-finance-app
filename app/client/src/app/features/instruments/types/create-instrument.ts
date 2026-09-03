import { InstrumentType } from '../../../core/types/instrument-type';

export type CreateInstrument = {
  type: InstrumentType;
  name: string;
  cutoffDate?: number;
};
