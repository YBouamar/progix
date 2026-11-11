/**
 * Coupe d'une dalle post-contrainte sur trois travées : le câble passe en fibre haute sur les appuis
 * et en fibre basse à mi-travée, entre deux ancrages. Dessin de marque, sans valeur de calcul.
 */
export function TraceCable({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 600 120" preserveAspectRatio="none" aria-hidden className={className}>
      {/* Dalle */}
      <rect x="6" y="34" width="588" height="40" rx="2" fill="none" stroke="currentColor" strokeOpacity="0.28" strokeWidth="1.5" vectorEffect="non-scaling-stroke" />
      {/* Poteaux */}
      {[6, 198, 402, 582].map((x) => (
        <rect key={x} x={x} y="74" width="12" height="46" fill="currentColor" fillOpacity="0.12" />
      ))}
      {/* Câble : haut sur appuis, bas à mi-travée */}
      <path d="M12 54 C 60 54, 70 68, 105 68 S 170 40, 204 40 S 265 68, 303 68 S 370 40, 408 40 S 470 68, 500 68 S 550 54, 588 54"
        fill="none" stroke="var(--color-gaine)" strokeWidth="2.5" strokeLinecap="round" vectorEffect="non-scaling-stroke" />
      {/* Ancrages */}
      <rect x="2" y="46" width="8" height="16" rx="1" fill="var(--color-gaine)" />
      <rect x="590" y="46" width="8" height="16" rx="1" fill="var(--color-gaine)" />
    </svg>
  );
}
