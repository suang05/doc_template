type ClassValue = string | number | boolean | null | undefined | ClassValue[];

export function cn(...classes: ClassValue[]): string {
  return classes
    .flat(Infinity as 1)
    .filter(Boolean)
    .join(" ");
}
