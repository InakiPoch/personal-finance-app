import { InstrumentType } from '../../../core/types/instrument-type';

/** `POST /v1/instruments` response: the id and echoed type of the created instrument. */
export type InstrumentCreated = {
  id: string;
  type: InstrumentType;
};
