package com.example.library;

import java.io.BufferedWriter;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.List;

/**
 * Report generation with StringBuffer, concatenated multi-line strings and
 * legacy java.io.File handling with manual close in finally.
 */
public class ReportWriter {

    public String buildHeader(String libraryName) {
        // Concatenated multi-line string: a text-block candidate.
        return """
                =========================================
                  Library Report
                  %s
                =========================================
                """.formatted(libraryName);
    }

    public String buildReport(String libraryName, List<Book> books) {
        StringBuilder buffer = new StringBuilder();
        buffer.append(buildHeader(libraryName));
        for (Book book : books) {
            buffer.append(String.format("%-40s %-25s %d%n",
                    book.getTitle(), book.getAuthor(), book.getYear()));
        }
        buffer.append("Total: ").append(books.size()).append(" book(s)\n");
        return buffer.toString();
    }

    public void writeToFile(String report, String path) throws IOException {
        Path file = Path.of(path);
        try (BufferedWriter writer = Files.newBufferedWriter(file, StandardCharsets.UTF_8)) {
            writer.write(report);
        }
    }
}
