package com.example.library;

import java.io.IOException;
import java.util.List;

public class Main {

    public static void main(String[] args) {
        final var library = new Library();

        library.addBook(new Book("978-0134685991", "Effective Java", "Joshua Bloch", 2018));
        library.addBook(new Book("978-0596009205", "Head First Java", "Kathy Sierra", 2005));
        library.addBook(new Book("978-0132350884", "Clean Code", "Robert Martin", 2008));
        library.addBook(new Book("978-0201633610", "Design Patterns", "Erich Gamma", 1994));
        library.addBook(new Book("978-0135957059", "The Pragmatic Programmer", "David Thomas", 2019));

        Member alice = new Member.Student("Alice", "State University");
        Member bob = new Member.Staff("Bob");

        library.checkout("978-0134685991", alice);
        library.checkout("978-0132350884", bob);

        List<String> summaries = library.getCatalog().stream()
                .map(book -> book.getTitle()
                        + " [" + library.categorize(book) + "] - "
                        + library.describeBorrower(book.getIsbn()))
                .toList();

        for (String summary : summaries) {
            System.out.println(summary);
        }
        System.out.println("Active loans: " + library.countLoans());

        Runnable reportJob = () -> {
            ReportWriter writer = new ReportWriter();
            String report = writer.buildReport("City Library", library.getCatalog());
            System.out.print(report);
            try {
                writer.writeToFile(report, "library-report.txt");
            } catch (IOException e) {
                System.err.println("Failed to write report: " + e.getMessage());
            }
        };
        reportJob.run();
    }
}
