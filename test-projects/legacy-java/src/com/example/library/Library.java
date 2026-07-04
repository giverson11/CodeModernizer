package com.example.library;

import java.util.Comparator;
import java.util.Hashtable;
import java.util.List;
import java.util.Vector;

/**
 * Core catalog written with legacy collections (Vector/Hashtable), explicit
 * iterators, anonymous inner classes and instanceof-and-cast chains.
 */
public class Library {

    private final Vector<Book> catalog = new Vector<>();
    private final Hashtable<String, Member> borrowers = new Hashtable<>();

    public void addBook(Book book) {
        catalog.add(book);
    }

    public boolean checkout(String isbn, Member member) {
        Book found = null;
        for (Book candidate : catalog) {
            if (candidate.getIsbn().equals(isbn)) {
                found = candidate;
                break;
            }
        }
        if (found == null) {
            return false;
        }
        if (borrowers.containsKey(isbn)) {
            return false;
        }
        borrowers.put(isbn, member);
        return true;
    }

    public String describeBorrower(String isbn) {
        Member member = borrowers.get(isbn);
        if (member == null) {
            return "available";
        }
        // Pattern matching switch replaces the instanceof-and-cast chain.
        return switch (member) {
            case Member.Student student -> "loaned to student " + student.getName() + " (" + student.getSchool() + ")";
            case Member.Staff staff -> "loaned to staff " + staff.getName();
            default -> "loaned to guest " + member.getName();
        };
    }

    public String categorize(Book book) {
        // Switch expression replaces the statement switch with breaks.
        int decade = (book.getYear() / 10) * 10;
        return switch (decade) {
            case 1950, 1960, 1970 -> "vintage";
            case 1980, 1990 -> "classic";
            case 2000, 2010 -> "modern";
            default -> "contemporary";
        };
    }

    public List<Book> booksByAuthor(String author) {
        Vector<Book> result = new Vector<>();
        for (Book book : catalog) {
            if (book.getAuthor().equalsIgnoreCase(author)) {
                result.add(book);
            }
        }
        // Lambda/method reference replaces the anonymous Comparator.
        result.sort(Comparator.comparingInt(Book::getYear));
        return result;
    }

    public int countLoans() {
        return borrowers.size();
    }

    public Vector<Book> getCatalog() {
        return catalog;
    }
}
