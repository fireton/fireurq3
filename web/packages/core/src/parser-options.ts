export const compatibilityMode = {
  dosUrq: "DosUrq"
} as const;

export type CompatibilityMode =
  (typeof compatibilityMode)[keyof typeof compatibilityMode];

export interface ParserOptions {
  compatibilityMode?: CompatibilityMode;
  allowUnknownCommands?: boolean;
}

export const defaultParserOptions: Required<ParserOptions> = {
  compatibilityMode: compatibilityMode.dosUrq,
  allowUnknownCommands: true
};
