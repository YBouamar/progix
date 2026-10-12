"use client";

import * as Dialog from "@radix-ui/react-dialog";
import { X } from "lucide-react";

/** Panneau latéral droit, utilisé pour les fiches et les formulaires. Avec « large », une grande fenêtre centrée pour les contenus à tableaux. */
export function Sheet({ open, onOpenChange, title, description, children, footer, large, plein }: {
  open: boolean; onOpenChange: (o: boolean) => void; title: string; description?: string;
  children: React.ReactNode; footer?: React.ReactNode; large?: boolean; plein?: boolean;
}) {
  return (
    <Dialog.Root open={open} onOpenChange={onOpenChange}>
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 z-40 bg-texte/40" />
        <Dialog.Content className={large
          ? "fixed left-1/2 top-1/2 z-50 flex h-[calc(100vh-48px)] w-[calc(100vw-48px)] max-w-[1280px] -translate-x-1/2 -translate-y-1/2 flex-col overflow-hidden rounded-2xl bg-white shadow-2xl focus:outline-none max-sm:h-screen max-sm:w-screen max-sm:rounded-none"
          : "fixed inset-y-0 right-0 z-50 flex w-full max-w-[640px] flex-col bg-white shadow-2xl focus:outline-none"}>
          <div className="flex items-start justify-between gap-4 border-b border-bordure px-6 py-4">
            <div>
              <Dialog.Title className="font-display text-lg font-bold">{title}</Dialog.Title>
              <Dialog.Description className="text-[13px] text-secondaire">{description ?? ""}</Dialog.Description>
            </div>
            <Dialog.Close className="rounded-lg p-2 text-secondaire hover:bg-neutre-fond" aria-label="Fermer">
              <X size={18} />
            </Dialog.Close>
          </div>
          {/* « plein » : le contenu gère lui-même sa mise en page et son défilement. */}
          <div className={plein ? "min-h-0 flex-1 overflow-hidden" : "flex-1 overflow-y-auto px-6 py-5"}>{children}</div>
          {footer && <div className="flex justify-end gap-2 border-t border-bordure px-6 py-4">{footer}</div>}
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
}
