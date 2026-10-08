from studio.webapp import main

if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        from pathlib import Path
        from tkinter import Tk, messagebox
        root = Tk()
        root.withdraw()
        messagebox.showerror("Mod Garage could not start", str(error), parent=root)
        root.destroy()
