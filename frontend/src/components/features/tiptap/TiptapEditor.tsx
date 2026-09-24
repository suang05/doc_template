"use client";
// Pattern: features/tiptap — Tiptap rich-text editor with {{variable}} token highlighting
import { useEditor, EditorContent, Extension } from "@tiptap/react";
import StarterKit from "@tiptap/starter-kit";
import { Plugin, PluginKey } from "@tiptap/pm/state";
import { Decoration, DecorationSet } from "@tiptap/pm/view";
import { useEffect, useCallback } from "react";
import { Bold, Italic, List, ListOrdered, Undo, Redo } from "lucide-react";
import { cn } from "@/utils/cn";

// ProseMirror plugin: visually highlight {{variable}} tokens without modifying the underlying HTML
const PlaceholderHighlight = Extension.create({
  name: "placeholderHighlight",
  addProseMirrorPlugins() {
    return [
      new Plugin({
        key: new PluginKey("placeholder-highlight"),
        props: {
          decorations(state) {
            const decos: Decoration[] = [];
            const pattern = /\{\{(\w+(?::\w+)*)\}\}/g;
            state.doc.descendants((node, pos) => {
              if (!node.isText || !node.text) return;
              let m: RegExpExecArray | null;
              pattern.lastIndex = 0;
              while ((m = pattern.exec(node.text)) !== null) {
                decos.push(
                  Decoration.inline(pos + m.index, pos + m.index + m[0].length, {
                    class: "tiptap-var-token",
                  })
                );
              }
            });
            return DecorationSet.create(state.doc, decos);
          },
        },
      }),
    ];
  },
});

interface TiptapEditorProps {
  value: string;
  onChange: (html: string) => void;
  placeholder?: string;
  className?: string;
  readOnly?: boolean;
}

export function TiptapEditor({ value, onChange, placeholder, className, readOnly = false }: TiptapEditorProps) {
  const editor = useEditor({
    extensions: [StarterKit, PlaceholderHighlight],
    content: value,
    editable: !readOnly,
    onUpdate: ({ editor }) => {
      onChange(editor.getHTML());
    },
  });

  // Sync external value into editor (e.g. template load)
  useEffect(() => {
    if (!editor) return;
    if (editor.getHTML() !== value) {
      editor.commands.setContent(value, { emitUpdate: false });
    }
  }, [value]); // eslint-disable-line react-hooks/exhaustive-deps

  const cmd = useCallback(
    (fn: () => boolean) => {
      editor?.chain().focus().run();
      fn();
    },
    [editor]
  );

  const btnCls = (active?: boolean) =>
    cn(
      "p-1.5 rounded-[var(--ri)] transition-colors",
      active
        ? "bg-[var(--blue-t)] text-[var(--blue)]"
        : "text-[var(--t3)] hover:text-[var(--t1)] hover:bg-[var(--sep)]"
    );

  return (
    <div className={cn("flex flex-col border border-[var(--border)] rounded-[var(--r)] bg-[var(--surf)] overflow-hidden", className)}>
      {/* Toolbar */}
      {!readOnly && (
        <div className="flex items-center gap-0.5 px-2 py-1.5 border-b border-[var(--sep)] bg-[var(--bg)]">
          <button
            type="button"
            onClick={() => editor?.chain().focus().toggleBold().run()}
            className={btnCls(editor?.isActive("bold"))}
            title="Bold"
          >
            <Bold className="w-3.5 h-3.5" />
          </button>
          <button
            type="button"
            onClick={() => editor?.chain().focus().toggleItalic().run()}
            className={btnCls(editor?.isActive("italic"))}
            title="Italic"
          >
            <Italic className="w-3.5 h-3.5" />
          </button>
          <div className="w-px h-4 bg-[var(--border)] mx-1" />
          <button
            type="button"
            onClick={() => editor?.chain().focus().toggleBulletList().run()}
            className={btnCls(editor?.isActive("bulletList"))}
            title="Bullet list"
          >
            <List className="w-3.5 h-3.5" />
          </button>
          <button
            type="button"
            onClick={() => editor?.chain().focus().toggleOrderedList().run()}
            className={btnCls(editor?.isActive("orderedList"))}
            title="Ordered list"
          >
            <ListOrdered className="w-3.5 h-3.5" />
          </button>
          <div className="w-px h-4 bg-[var(--border)] mx-1" />
          <button
            type="button"
            onClick={() => editor?.chain().focus().undo().run()}
            disabled={!editor?.can().undo()}
            className={btnCls()}
            title="Undo"
          >
            <Undo className="w-3.5 h-3.5" />
          </button>
          <button
            type="button"
            onClick={() => editor?.chain().focus().redo().run()}
            disabled={!editor?.can().redo()}
            className={btnCls()}
            title="Redo"
          >
            <Redo className="w-3.5 h-3.5" />
          </button>
          <div className="flex-1" />
          <span className="text-t-xs text-[var(--t3)] italic">
            พิมพ์ <code className="font-mono bg-[var(--sep)] px-1 rounded">{"{{ชื่อตัวแปร}}"}</code> เพื่อแทรกตัวแปร
          </span>
        </div>
      )}

      {/* Editor area */}
      <style>{`
        .tiptap-var-token {
          background: var(--blue-t);
          border: 1px solid var(--blue);
          color: var(--blue);
          border-radius: 4px;
          padding: 0 3px;
          font-family: monospace;
          font-size: 0.82em;
          font-weight: 600;
          white-space: nowrap;
        }
        .tiptap-editor-content .ProseMirror {
          outline: none;
          min-height: 200px;
          padding: 12px 14px;
          font-size: 0.875rem;
          line-height: 1.6;
          color: var(--t1);
        }
        .tiptap-editor-content .ProseMirror p { margin: 0 0 0.5em; }
        .tiptap-editor-content .ProseMirror ul,
        .tiptap-editor-content .ProseMirror ol { margin: 0 0 0.5em; padding-left: 1.5em; }
        .tiptap-editor-content .ProseMirror li { margin-bottom: 0.2em; }
        .tiptap-editor-content .ProseMirror p.is-editor-empty:first-child::before {
          content: attr(data-placeholder);
          color: var(--t3);
          pointer-events: none;
          float: left;
          height: 0;
        }
      `}</style>
      <div className="tiptap-editor-content flex-1 overflow-auto">
        <EditorContent
          editor={editor}
          data-placeholder={placeholder ?? "เริ่มพิมพ์เนื้อหาเอกสาร..."}
        />
      </div>
    </div>
  );
}
